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
// Sentry's address for the API's error reports. Public by design, like the app's in appsettings.Production.json
param sentryDsn = 'https://b03541a8513be443b7fde8b1c48bbe73@o4512239109799936.ingest.de.sentry.io/4512239182938192'

// In Danish kroner, since that is the subscription's currency. 50 kr. is about $8, so about $100 a year,
// which is what the student credit covers
param monthlyBudget = 50
param budgetStartDate = '2026-09-01'
param budgetEmails = [
  'support@stikling.app'
]
