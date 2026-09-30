// Everything the hosted sync API needs on Azure: a resource group with the API and its storage in
// it, and a budget on the subscription. docs/hosting.md says how to deploy it and in what order.

targetScope = 'subscription'

@description('The Azure region. The Azure for Students subscription only allows a few, and Sweden Central is the closest of them.')
param location string = 'swedencentral'

@description('Where the app is served from. The API only accepts sign-ins from here.')
param appDomain string

@description('The API\'s own address. Empty while its DNS records aren\'t set up yet.')
param apiDomain string = ''

@description('False the first time the API\'s domain is added, which asks Azure for the certificate. True from then on, which uses it.')
param apiCertificateIssued bool = false

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
    clerkAuthority: clerkAuthority
    githubRepository: githubRepository
    apiImage: apiImage
    apiMinReplicas: apiMinReplicas
  }
}

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
      allSpent: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: budgetEmails
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
output deployClientId string = resources.outputs.deployClientId
