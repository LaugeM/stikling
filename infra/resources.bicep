// The API, its database and its photo storage, all in one resource group. Nothing here has a
// password: the API reaches the database and storage with its own managed identity, and GitHub
// Actions deploys with another one that trusts the repository's main branch.

param location string
param appDomain string
param apiDomain string
param apiCertificateIssued bool
param clerkAuthority string
param githubRepository string
param apiImage string
param apiMinReplicas int

// Storage account and SQL server names are global, so they get a part that is fixed per resource group
var suffix = uniqueString(resourceGroup().id)

// Built-in roles
var storageBlobDataContributor = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var contributor = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
var reader = 'acdd72a7-3385-48ef-bd42-f606fba81ae7'
var managedIdentityOperator = 'f1a07417-d97a-45cb-824c-7a7467783830'

// Identities

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-stikling-api'
  location: location
}

resource deployIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-stikling-deploy'
  location: location

  // GitHub Actions signs in as this identity from the main branch, without a stored secret
  resource github 'federatedIdentityCredentials' = {
    name: 'github-main'
    properties: {
      issuer: 'https://token.actions.githubusercontent.com'
      subject: 'repo:${githubRepository}:ref:refs/heads/main'
      audiences: [
        'api://AzureADTokenExchange'
      ]
    }
  }
}

// Photos

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'ststikling${suffix}'
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }

  resource blobs 'blobServices' = {
    name: 'default'
    properties: {
      // A deleted photo is gone straight away, as the privacy page says
      deleteRetentionPolicy: {
        enabled: false
      }
      containerDeleteRetentionPolicy: {
        enabled: false
      }
    }

    resource photos 'containers' = {
      name: 'photos'
    }
  }
}

resource apiCanUseStorage 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, apiIdentity.id, storageBlobDataContributor)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributor)
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Database

resource sql 'Microsoft.Sql/servers@2023-08-01' = {
  name: 'sql-stikling-${suffix}'
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    // Only Microsoft Entra sign-ins, with the API as the admin. The API applies its own migrations
    // when it starts, and nothing else is in this database.
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: apiIdentity.name
      principalType: 'Application'
      sid: apiIdentity.properties.clientId
      tenantId: subscription().tenantId
    }
  }

  // Container Apps on the consumption plan have no fixed address, so the server lets Azure's own
  // services through. Every sign-in still has to be the API's identity.
  resource azure 'firewallRules' = {
    name: 'AllowAllWindowsAzureIps'
    properties: {
      startIpAddress: '0.0.0.0'
      endIpAddress: '0.0.0.0'
    }
  }

  // On the Basic tier, which is always on. The free serverless offer paused after about 20 minutes
  // without use and took close to a minute to wake, so most syncs after a break waited that long.
  // The API woke it on every start, bots included, which used most of the month's free amount in
  // the first four days of October. Basic costs about $5 a month and holds up to 2 GB. A database
  // can't go back to the free offer once it has been on a paid tier.
  resource database 'databases' = {
    name: 'stikling'
    location: location
    sku: {
      name: 'Basic'
      tier: 'Basic'
      capacity: 5
    }
    properties: {
      useFreeLimit: false
      maxSizeBytes: 2147483648
      requestedBackupStorageRedundancy: 'Local'
    }

    // The privacy page says deleted data is gone from the backups after 7 days
    resource backups 'backupShortTermRetentionPolicies' = {
      name: 'default'
      properties: {
        retentionDays: 7
      }
    }
  }
}

// The API

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-stikling'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    // The privacy page says the API's logs are kept for 30 days
    retentionInDays: 30
    // The first 5 GB a month are free, and the API writes far less than that. The cap keeps a
    // flood of requests from turning into a bill.
    workspaceCapping: {
      dailyQuotaGb: json('0.15')
    }
  }
}

// A newer API version than the rest, since it's the one that takes environmentMode. Bicep has no
// types for it yet, so it warns that it can't check the properties.
#disable-next-line BCP081
resource environment 'Microsoft.App/managedEnvironments@2026-07-01' = {
  name: 'cae-stikling'
  location: location
  properties: {
    // Left out, Azure makes an "express" environment, which can't have a custom domain. The
    // Consumption profile has the same pricing and free grant.
    environmentMode: 'WorkloadProfiles'
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

var certificateName = 'api-domain'

// The certificate is asked for once the domain is on the app, so it can't be referred to by symbol
// from the app without the two depending on each other
var certificateId = '${environment.id}/managedCertificates/${certificateName}'

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'stikling-api'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false
        customDomains: empty(apiDomain)
          ? []
          : [
              {
                name: apiDomain
                bindingType: apiCertificateIssued ? 'SniEnabled' : 'Disabled'
                certificateId: apiCertificateIssued ? certificateId : null
              }
            ]
      }
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              // Opening a connection retries for a minute, which covers the database moving between
              // tiers, and waking if it is ever put back on the free tier that pauses
              name: 'ConnectionStrings__Stikling'
              value: 'Server=tcp:${sql.properties.fullyQualifiedDomainName},1433;Database=${sql::database.name};Authentication=Active Directory Managed Identity;User Id=${apiIdentity.properties.clientId};Encrypt=True;Connect Retry Count=6;Connect Retry Interval=10'
            }
            {
              name: 'ConnectionStrings__Photos'
              value: storage.properties.primaryEndpoints.blob
            }
            {
              name: 'Clerk__Authority'
              value: clerkAuthority
            }
            {
              name: 'AppOrigins__0'
              value: 'https://${appDomain}'
            }
            {
              // Which identity the storage client signs in as, and that it only tries that one
              name: 'AZURE_CLIENT_ID'
              value: apiIdentity.properties.clientId
            }
            {
              name: 'AZURE_TOKEN_CREDENTIALS'
              value: 'ManagedIdentityCredential'
            }
          ]
        }
      ]
      scale: {
        minReplicas: apiMinReplicas
        // One copy is plenty, and it keeps the database migrations on start to one at a time
        maxReplicas: 1
      }
    }
  }
  dependsOn: [
    apiCanUseStorage
  ]
}

resource certificate 'Microsoft.App/managedEnvironments/managedCertificates@2024-03-01' = if (!empty(apiDomain)) {
  parent: environment
  name: certificateName
  location: location
  properties: {
    subjectName: apiDomain
    domainControlValidation: 'CNAME'
  }
  dependsOn: [
    api
  ]
}

// The deploy workflow may change the API's container app and nothing outside it. Updating the app
// also means joining it to its environment and handing it its identity again, so it may do those two.
resource deployCanUpdateApi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(api.id, deployIdentity.id, contributor)
  scope: api
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributor)
    principalId: deployIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource deployCanJoinEnvironment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(environment.id, deployIdentity.id, contributor)
  scope: environment
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributor)
    principalId: deployIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource deployCanAssignApiIdentity 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(apiIdentity.id, deployIdentity.id, managedIdentityOperator)
  scope: apiIdentity
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', managedIdentityOperator)
    principalId: deployIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource deployCanReadGroup 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, deployIdentity.id, reader)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', reader)
    principalId: deployIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output apiAddress string = empty(apiDomain) ? 'https://${api.properties.configuration.ingress.fqdn}' : 'https://${apiDomain}'
output apiDnsTarget string = api.properties.configuration.ingress.fqdn
output apiDomainVerificationId string = api.properties.customDomainVerificationId
output deployClientId string = deployIdentity.properties.clientId
output apiName string = api.name
