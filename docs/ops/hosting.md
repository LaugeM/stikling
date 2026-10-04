# Hosting

The app is static files on GitHub Pages at https://stikling.app. The sync API runs on Azure, described in Bicep in `infra/`:

- **Container Apps** runs the API from the image the workflow pushes to `ghcr.io/laugem/stikling-api`. It sleeps when nobody uses it, so the first request after a quiet spell waits a few seconds. `apiMinReplicas = 1` in `infra/main.bicepparam` keeps one copy awake instead, for about $4 a month. Together with the database that comes to more than the $8 a month budget, so it stays at 0.
- **Azure SQL** on the Basic tier, about $5 a month, which is always on and holds up to 2 GB. It used to be on the free serverless offer, but that paused after about 20 minutes without use and took close to a minute to wake, so a sync after a break waited that long. Each time the API started it woke the database too, so bots hitting the API used most of the month's free amount in the first four days of October.
- **Log Analytics** for the API's logs, capped at 0.15 GB a day so it stays inside the free 5 GB a month.
- **Blob Storage** for the photo images.
- **A budget** of $8 a month on the subscription, which is $96 a year against the $100 a year from Azure for Students. It emails support@stikling.app at half and all of the monthly amount, and runs the cost stop below.

Nothing has a password. The API signs in to the database and storage as its own managed identity (`id-stikling-api`), which is also the database's admin, so the API applies its migrations when it starts. GitHub Actions deploys as another identity (`id-stikling-deploy`) that trusts the main branch of this repository. It can change the Container App and its environment, but not the database, the storage or who has access to them.

Sign-in goes through Clerk's production instance on `clerk.stikling.app`, with Google, Facebook and Discord set up with our own credentials from Google Cloud, Meta for Developers and Discord's developer portal. The free Clerk plan allows three social sign-ins.

## The cost stop

A budget can only send emails, and when the Azure for Students credit runs out, the whole subscription is switched off. So `infra/cost-stop.bicep` turns the paid parts off before that can happen:

- When the month's budget is spent, a Logic App moves the database to the free tier. Sync is slow again but keeps working, and the data stays as it is.
- At 125% of the budget ($10), another one stops the API. The app keeps working on each device, and changes wait there until the API is back.
- At 02:00 UTC on the 1st of each month, a third one moves the database back to Basic and starts the API, if they aren't like that already. That also undoes a change made by hand.

Every change the stop makes sends an email to the budget's addresses, and so does one that fails. Azure works out costs several hours late, so the stop comes up to a day after the line is crossed.

While the API is stopped, a new version can't be checked after it's deployed, so the workflow's last step fails. To bring things back before the 1st, run `logic-stikling-month-start` from the portal (Run trigger, on its overview page). Deploying the Bicep also moves the database back to Basic, but doesn't start the API.

## Deploying a new version

Merging to main does it. The workflow pushes the API's image and moves the Container App to it, and GitHub Pages gets the app.

## Changing the Azure resources

The Bicep isn't deployed by the workflow, since that would need the right to hand out roles. Deploy it from your own machine after `az login`:

```bash
az deployment sub create --location polandcentral --template-file infra/main.bicep --parameters infra/main.bicepparam
```

## Setting it up from nothing

This is also the way to move it to another subscription or region. For another region, change `location` in `infra/main.bicep` and delete `rg-stikling` first, since a resource group can't move and the storage and SQL names would clash with the old ones.

`githubRepository` in `infra/main.bicepparam` has the ids GitHub puts in the Actions sign-in token, as `owner@ownerId/name@repositoryId`. They come from `gh api repos/LaugeM/stikling --jq '.owner.id, .id'`. A new or renamed repository has to update it, or the API deploy fails to sign in to Azure.

1. Register the resource providers once:
   `az provider register --namespace Microsoft.App`, and the same for `Microsoft.Sql`, `Microsoft.Storage`, `Microsoft.OperationalInsights`, `Microsoft.ManagedIdentity`, `Microsoft.Logic` and `Microsoft.Insights`.
2. Deploy without the API's domain, and with a sample image, since the workflow hasn't pushed one yet:
   add `--parameters apiDomain='' apiImage=mcr.microsoft.com/dotnet/samples:aspnetapp` to the command above. The outputs have the values for the next two steps.
3. At the DNS host for stikling.app, add a CNAME record `api` pointing at `apiDnsTarget`, and a TXT record `asuid.api` with `apiDomainVerificationId`.
4. Deploy again with `apiCertificateIssued=false` and the sample image, which adds the domain and asks for its certificate. When the certificate shows as succeeded under the Container Apps environment in the portal, deploy once more with only the sample image, which uses it.
5. In the repository settings, under Secrets and variables, then Actions, add the secrets `AZURE_CLIENT_ID` (the `deployClientId` output), `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID`. They aren't passwords, but as secrets they're hidden in the workflow's logs, which anyone can read. Until they're there, the API deploy fails and the rest of the workflow carries on.
6. Run the workflow on main. The first time, the API deploy fails because the image it just pushed is private. Make the `stikling-api` package public under Packages on GitHub, so Container Apps can pull it without a password, and run the failed job again.

From then on the command above works with the defaults.

For the app itself, the domain is set under Pages in the repository settings, with the four A records GitHub lists for an apex domain.

## Looking at the database

Only the API's identity can sign in to the database. To look at it yourself, make your own account the admin for a while under Microsoft Entra ID on the SQL server in the portal, and set it back to `id-stikling-api` afterwards. The API can't reach the database while you are the admin.
