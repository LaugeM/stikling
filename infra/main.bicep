// Everything the hosted sync API needs on Azure: a resource group with the API and its storage in
// it, and a budget on the subscription. docs/ops/hosting.md says how to deploy it and in what order.

targetScope = 'subscription'

@description('The Azure region. The Azure for Students subscription only allows a few. Poland Central is used because Sweden Central had no room for a new Container Apps environment.')
param location string = 'polandcentral'

@description('Where the app is served from. The API only accepts sign-ins from here.')
param appDomain string

@description('The API\'s own address. Empty while its DNS records aren\'t set up yet.')
param apiDomain string = ''

@description('False the first time the API\'s domain is added, which asks Azure for the certificate. True from then on, which uses it.')
param apiCertificateIssued bool = false

@description('Where the public pages of share links are shown, like share.stikling.app. It points at the same container app as the API. Empty while its DNS records aren\'t set up yet.')
param shareDomain string = ''

@description('False the first time the share domain is added, which asks Azure for the certificate. True from then on, which uses it.')
param shareCertificateIssued bool = false

@description('The Frontend API URL of the Clerk production instance.')
param clerkAuthority string

@description('The repository whose main branch deploys the API, as GitHub names it in the sign-in token: owner@ownerId/name@repositoryId. The ids keep a new repository with the same name from signing in.')
param githubRepository string

@description('The API\'s container image. The deploy workflow moves it on to each new version after this.')
param apiImage string

@description('The number of copies of the API kept running when nobody uses it. 0 lets it sleep, which is free.')
@minValue(0)
@maxValue(1)
param apiMinReplicas int = 0

@description('The monthly spending, in the subscription\'s currency, that the budget warns about.')
param monthlyBudget int

@description('The first day of the month the budget starts in, as yyyy-MM-dd. It can\'t be moved once the budget exists.')
param budgetStartDate string

@description('Who gets the budget\'s warnings.')
param budgetEmails array

resource group 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-stikling'
  location: location
}

module resources 'resources.bicep' = {
  name: 'stikling-resources'
  scope: group
  params: {
    location: location
    appDomain: appDomain
    apiDomain: apiDomain
    apiCertificateIssued: apiCertificateIssued
    shareDomain: shareDomain
    shareCertificateIssued: shareCertificateIssued
    clerkAuthority: clerkAuthority
    githubRepository: githubRepository
    apiImage: apiImage
    apiMinReplicas: apiMinReplicas
  }
}

module costStop 'cost-stop.bicep' = {
  name: 'stikling-cost-stop'
  scope: group
  params: {
    location: location
    apiName: resources.outputs.apiName
    emails: budgetEmails
  }
}

// Warns by email, and calls the cost stop in cost-stop.bicep when the month's budget is spent
resource budget 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: 'stikling'
  properties: {
    category: 'Cost'
    amount: monthlyBudget
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: budgetStartDate
    }
    notifications: {
      halfSpent: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 50
        thresholdType: 'Actual'
        contactEmails: budgetEmails
      }
      // The API stops
      allSpent: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: budgetEmails
        contactGroups: [
          costStop.outputs.stopApiActionGroupId
        ]
      }
      headingOver: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: budgetEmails
      }
    }
  }
}

output apiAddress string = resources.outputs.apiAddress
output apiDnsTarget string = resources.outputs.apiDnsTarget
output apiDomainVerificationId string = resources.outputs.apiDomainVerificationId
output shareDnsTarget string = resources.outputs.apiDnsTarget
output shareDomainVerificationId string = resources.outputs.apiDomainVerificationId
output deployClientId string = resources.outputs.deployClientId
