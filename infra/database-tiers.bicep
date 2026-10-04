// The two tiers the database can be on. It's on Basic, and the cost stop in cost-stop.bicep moves
// it to the free one when the month's budget is spent, and back at the start of the next month.
// Both are written as the body of a request to change the database, so the cost stop can send them
// as they are.

@export()
@description('Always on, about $5 a month, and holds up to 2 GB.')
var basicTier = {
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    useFreeLimit: false
    maxSizeBytes: 2147483648
  }
}

@export()
@description('''
The free serverless offer. It pauses after a while without use and takes close to a minute to wake,
so syncs are slow on it. If the month's free amount runs out, it pauses until the next month instead
of costing anything.
''')
var freeTier = {
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
    autoPauseDelay: 60
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368
  }
}
