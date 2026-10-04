// The cost stop. A budget only sends emails, and when the Azure for Students credit runs out the
// whole subscription is switched off, so this turns the paid parts off before that can happen. The
// budget in main.bicep calls the two action groups here:
//
// - When the month's budget is spent, the database moves to the free tier. Sync is slow again but
//   keeps working.
// - At 125% of it, the API is stopped. The app keeps working on each device, and changes wait there
//   until it is back.
//
// On the 1st of each month, both are undone. Each change the stop makes sends an email, and so does
// one that fails.
//
// Azure works out costs several hours late, so the stop comes up to a day after the line is crossed.

import { basicTier, freeTier } from 'database-tiers.bicep'

param location string
param sqlServerName string
param databaseName string
param apiName string

@description('Who gets an email when the cost stop changes something.')
param emails array

// Built-in role
var sqlDbContributor = '9b7fa17d-e63e-47b0-bb0a-15c516ac86ec'

resource sql 'Microsoft.Sql/servers@2023-08-01' existing = {
  name: sqlServerName

  resource database 'databases' existing = {
    name: databaseName
  }
}

resource api 'Microsoft.App/containerApps@2024-03-01' existing = {
  name: apiName
}

// The Logic Apps act as this identity, which may change the database's tier and stop or start the
// API, but can't read the data in either
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-stikling-cost-stop'
  location: location
}

resource canChangeDatabase 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(sql::database.id, identity.id, sqlDbContributor)
  scope: sql::database
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', sqlDbContributor)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Only what the stop needs, so it can't change the API's settings or read its secrets
resource stopAndStartApi 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(resourceGroup().id, 'stikling-stop-and-start-api')
  properties: {
    roleName: 'Stikling cost stop'
    description: 'Can see whether the API is running, and stop or start it.'
    type: 'CustomRole'
    assignableScopes: [
      resourceGroup().id
    ]
    permissions: [
      {
        actions: [
          'Microsoft.App/containerApps/read'
          'Microsoft.App/containerApps/stop/action'
          'Microsoft.App/containerApps/start/action'
        ]
      }
    ]
  }
}

resource canStopApi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(api.id, identity.id, stopAndStartApi.id)
  scope: api
  properties: {
    roleDefinitionId: stopAndStartApi.id
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// What the Logic Apps send to Azure's management API

var authentication = {
  type: 'ManagedServiceIdentity'
  identity: identity.id
  audience: environment().resourceManager
}

var databaseAddress = '${environment().resourceManager}${skip(sql::database.id, 1)}?api-version=2023-08-01'
var apiAddress = '${environment().resourceManager}${skip(api.id, 1)}'
var apiVersion = '?api-version=2024-03-01'

var schema = 'https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#'

var calledByBudget = {
  manual: {
    type: 'Request'
    kind: 'Http'
    inputs: {
      schema: {}
    }
  }
}

resource freeDatabase 'Microsoft.Logic/workflows@2019-05-01' = {
  name: 'logic-stikling-free-database'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    definition: {
      '$schema': schema
      contentVersion: '1.0.0.0'
      triggers: calledByBudget
      actions: {
        Move_the_database_to_the_free_tier: {
          type: 'Http'
          inputs: {
            method: 'PATCH'
            uri: databaseAddress
            body: freeTier
            authentication: authentication
          }
          runAfter: {}
        }
      }
    }
  }
}

resource stopApi 'Microsoft.Logic/workflows@2019-05-01' = {
  name: 'logic-stikling-stop-api'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    definition: {
      '$schema': schema
      contentVersion: '1.0.0.0'
      triggers: calledByBudget
      actions: {
        Stop_the_API: {
          type: 'Http'
          inputs: {
            method: 'POST'
            uri: '${apiAddress}/stop${apiVersion}'
            authentication: authentication
          }
          runAfter: {}
        }
      }
    }
  }
}

// Only changes what isn't as it should be, so a month without a stop sends no email. It also undoes
// a change made by hand, like a stopped API.
resource monthStart 'Microsoft.Logic/workflows@2019-05-01' = {
  name: 'logic-stikling-month-start'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    definition: {
      '$schema': schema
      contentVersion: '1.0.0.0'
      triggers: {
        // A little after the budget's month starts, which is at midnight UTC
        Each_month: {
          type: 'Recurrence'
          recurrence: {
            frequency: 'Month'
            interval: 1
            startTime: '2026-11-01T02:00:00Z'
          }
        }
      }
      actions: {
        Get_the_database: {
          type: 'Http'
          inputs: {
            method: 'GET'
            uri: databaseAddress
            authentication: authentication
          }
          runAfter: {}
        }
        If_the_database_isnt_on_Basic: {
          type: 'If'
          expression: {
            not: {
              equals: [
                '@body(\'Get_the_database\')?[\'sku\']?[\'name\']'
                basicTier.sku.name
              ]
            }
          }
          actions: {
            Move_the_database_to_Basic: {
              type: 'Http'
              inputs: {
                method: 'PATCH'
                uri: databaseAddress
                body: basicTier
                authentication: authentication
              }
              runAfter: {}
            }
          }
          runAfter: {
            Get_the_database: [
              'Succeeded'
            ]
          }
        }
        Get_the_API: {
          type: 'Http'
          inputs: {
            method: 'GET'
            uri: '${apiAddress}${apiVersion}'
            authentication: authentication
          }
          runAfter: {}
        }
        // Stopped shows as "Stopped", but anything other than running is worth a start
        If_the_API_isnt_running: {
          type: 'If'
          expression: {
            not: {
              equals: [
                '@body(\'Get_the_API\')?[\'properties\']?[\'runningStatus\']'
                'Running'
              ]
            }
          }
          actions: {
            Start_the_API: {
              type: 'Http'
              inputs: {
                method: 'POST'
                uri: '${apiAddress}/start${apiVersion}'
                authentication: authentication
              }
              runAfter: {}
            }
          }
          runAfter: {
            Get_the_API: [
              'Succeeded'
            ]
          }
        }
      }
    }
  }
}

// What the budget calls

resource freeDatabaseGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-stikling-free-database'
  location: 'global'
  properties: {
    enabled: true
    groupShortName: 'StikFreeDb'
    logicAppReceivers: [
      {
        name: 'free-database'
        resourceId: freeDatabase.id
        callbackUrl: listCallbackUrl('${freeDatabase.id}/triggers/manual', '2019-05-01').value
        useCommonAlertSchema: true
      }
    ]
  }
}

resource stopApiGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-stikling-stop-api'
  location: 'global'
  properties: {
    enabled: true
    groupShortName: 'StikStopApi'
    logicAppReceivers: [
      {
        name: 'stop-api'
        resourceId: stopApi.id
        callbackUrl: listCallbackUrl('${stopApi.id}/triggers/manual', '2019-05-01').value
        useCommonAlertSchema: true
      }
    ]
  }
}

// An email for each change the stop makes, found in the activity log by the identity that made it.
// One that fails sends an email too, so a stop that didn't work isn't missed.

resource emailGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-stikling-cost-stop-email'
  location: 'global'
  properties: {
    enabled: true
    groupShortName: 'StikCostMail'
    emailReceivers: [
      for (email, i) in emails: {
        name: 'email-${i}'
        emailAddress: email
        useCommonAlertSchema: true
      }
    ]
  }
}

resource changed 'Microsoft.Insights/activityLogAlerts@2020-10-01' = {
  name: 'stikling-cost-stop-changed-something'
  location: 'global'
  properties: {
    enabled: true
    description: 'The cost stop changed the database\'s tier, or stopped or started the API.'
    scopes: [
      resourceGroup().id
    ]
    condition: {
      allOf: [
        {
          field: 'category'
          equals: 'Administrative'
        }
        {
          field: 'caller'
          equals: identity.properties.principalId
        }
        {
          anyOf: [
            {
              field: 'status'
              equals: 'Succeeded'
            }
            {
              field: 'status'
              equals: 'Failed'
            }
          ]
        }
      ]
    }
    actions: {
      actionGroups: [
        {
          actionGroupId: emailGroup.id
        }
      ]
    }
  }
}

output freeDatabaseActionGroupId string = freeDatabaseGroup.id
output stopApiActionGroupId string = stopApiGroup.id
