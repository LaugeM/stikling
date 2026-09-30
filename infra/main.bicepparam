using 'main.bicep'

param appDomain = 'stikling.app'
param apiDomain = 'api.stikling.app'
param apiCertificateIssued = true
param clerkAuthority = 'https://clerk.stikling.app'
param githubRepository = 'LaugeM/stikling'
param apiImage = 'ghcr.io/laugem/stikling-api:main'
param apiMinReplicas = 0

// About $100 a year, which is what the student credit covers
param monthlyBudget = 8
param budgetStartDate = '2026-09-01'
param budgetEmails = [
  'support@stikling.app'
]
