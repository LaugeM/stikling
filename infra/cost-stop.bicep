// The cost stop. A budget only sends emails, and when the Azure for Students credit runs out the
// whole subscription is switched off. So when the month's budget is spent, the budget in main.bicep
// calls the action group here, and a Logic App stops the API. The app keeps working on each device,
// and changes wait there until the API is back. On the 1st of each month another Logic App starts
// it again. The notification job (when there is one) is switched to manual by the stop, so it stops
// running every 15 minutes, and back to its schedule by the start. Each stop and start of the API
// sends an email, and so does one that fails, the job's switches included.
//
// The database can't be stopped: Basic is always on, and a database that has been on a paid tier
// can't go back to the free offer. Azure works out costs several hours late, so the stop comes up
// to a day after the line is crossed.

param location string
param apiName string

@description('The notification job. Empty while notifications are off and there is none.')
param jobName string

@description('The Container Apps environment, which updating the job joins it to again.')
param environmentName string

@description('The API\'s identity, which updating the job hands to it again.')
param apiIdentityName string

@description('Who gets an email when the cost stop changes something.')
param emails array

resource api 'Microsoft.App/containerApps@2024-03-01' existing = {
  name: apiName
}

resource appEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: environmentName
}

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: apiIdentityName
}

var hasJob = !empty(jobName)

// Never read while there is no job, but a resource has to have a name
resource job 'Microsoft.App/jobs@2024-03-01' existing = {
  name: hasJob ? jobName : 'none'
}

// The Logic Apps act as this identity, which may only stop and start the API
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-stikling-cost-stop'
  location: location
}

// Only what the stop needs, so it can't change the API's settings or read its secrets. Changing the
// job's trigger is an update of the job, which also joins it to its environment and hands it the
// API's identity again, so those two are included like they are for the deploy workflow.
resource stopAndStartApi 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(resourceGroup().id, 'stikling-stop-and-start-api')
  properties: {
    roleName: 'Stikling cost stop'
    description: 'Can see whether the API is running, stop or start it, and switch the notification job between manual and its schedule.'
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
          'Microsoft.App/jobs/read'
          'Microsoft.App/jobs/write'
          'Microsoft.App/managedEnvironments/join/action'
          'Microsoft.ManagedIdentity/userAssignedIdentities/assign/action'
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

resource canSwitchJob 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (hasJob) {
  name: guid(resourceGroup().id, 'stikling-notify', identity.id, stopAndStartApi.id)
  scope: job
  properties: {
    roleDefinitionId: stopAndStartApi.id
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource canJoinEnvironment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appEnvironment.id, identity.id, stopAndStartApi.id)
  scope: appEnvironment
  properties: {
    roleDefinitionId: stopAndStartApi.id
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource canAssignApiIdentity 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(apiIdentity.id, identity.id, stopAndStartApi.id)
  scope: apiIdentity
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

var apiAddress = '${environment().resourceManager}${skip(api.id, 1)}'
var apiVersion = '?api-version=2024-03-01'

// The job's address, and what switches it between the two ways it can be started
var jobAddress = hasJob ? '${environment().resourceManager}${skip(job.id, 1)}' : ''

var jobToManual = {
  properties: {
    configuration: {
      triggerType: 'Manual'
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
    }
  }
}

// The same schedule as in resources.bicep
var jobToSchedule = {
  properties: {
    configuration: {
      triggerType: 'Schedule'
      scheduleTriggerConfig: {
        cronExpression: '*/15 * * * *'
        parallelism: 1
        replicaCompletionCount: 1
      }
    }
  }
}

var switchToManual = {
  type: 'Http'
  inputs: {
    method: 'PATCH'
    uri: '${jobAddress}${apiVersion}'
    authentication: authentication
    headers: {
      'Content-Type': 'application/json'
    }
    body: jobToManual
  }
  runAfter: {}
}

var switchToSchedule = {
  type: 'Http'
  inputs: {
    method: 'PATCH'
    uri: '${jobAddress}${apiVersion}'
    authentication: authentication
    headers: {
      'Content-Type': 'application/json'
    }
    body: jobToSchedule
  }
  runAfter: {}
}

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
      // The job's switch is its own action next to the API's stop, so one failing doesn't hold back the other
      actions: union(
        {
          Stop_the_API: {
            type: 'Http'
            inputs: {
              method: 'POST'
              uri: '${apiAddress}/stop${apiVersion}'
              authentication: authentication
            }
            runAfter: {}
          }
        },
        hasJob ? { Switch_the_job_to_manual: switchToManual } : {}
      )
    }
  }
}

// Only starts the API when it isn't running, so a month without a stop sends no email. It also
// undoes a stop made by hand. The job is put back on its schedule every month, whether or not the
// API was stopped, so a deploy by hand can't leave it on manual for long. Doing it when it already
// is on its schedule changes nothing.
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
      actions: union({
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
      }, hasJob ? { Switch_the_job_to_its_schedule: switchToSchedule } : {})
    }
  }
}

// What the budget calls

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

// An email for each stop and start of the API, found in the activity log by the identity that made it. One that
// fails sends an email too, so a stop that didn't work isn't missed.

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
    description: 'The cost stop stopped or started the API, or tried to and failed.'
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
        // Azure also logs a policy check under the same identity each time, which isn't worth an email
        {
          anyOf: [
            {
              field: 'operationName'
              equals: 'Microsoft.App/containerApps/stop/action'
            }
            {
              field: 'operationName'
              equals: 'Microsoft.App/containerApps/start/action'
            }
          ]
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

// Switching the job happens every month, so only a switch that fails is worth an email
resource jobSwitchFailed 'Microsoft.Insights/activityLogAlerts@2020-10-01' = if (hasJob) {
  name: 'stikling-cost-stop-job-switch-failed'
  location: 'global'
  properties: {
    enabled: true
    description: 'The cost stop tried to switch the notification job between manual and its schedule, and failed.'
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
          field: 'operationName'
          equals: 'Microsoft.App/jobs/write'
        }
        {
          field: 'status'
          equals: 'Failed'
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

output stopApiActionGroupId string = stopApiGroup.id
