using 'main.bicep'

param appDomain = 'stikling.app'
param apiDomain = 'api.stikling.app'
param apiCertificateIssued = true
param shareDomain = 'share.stikling.app'
param shareCertificateIssued = true
param clerkAuthority = 'https://clerk.stikling.app'
param githubRepository = 'LaugeM@46719124/stikling@1377567106'
param apiImage = 'ghcr.io/laugem/stikling-api:main'
param apiMinReplicas = 0

// In Danish kroner, since that is the subscription's currency. 50 kr. is about $8, so about $100 a year,
// which is what the student credit covers
param monthlyBudget = 50
param budgetStartDate = '2026-09-01'
param budgetEmails = [
  'support@stikling.app'
]
