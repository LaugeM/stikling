# Hosting

The app is static files on GitHub Pages at https://stikling.app. The sync API runs on Azure, described in Bicep in `infra/`:

- **Container Apps** runs the API from the image the workflow pushes to `ghcr.io/laugem/stikling-api`. It answers on two names, `api.stikling.app` for the app and `share.stikling.app` for the public pages of share links. They are the same container app, and `Share__Host` and `Share__BaseUrl` tell the API which is which. It sleeps when nobody uses it, so the first request after a quiet spell waits a few seconds. `apiMinReplicas = 1` in `infra/main.bicepparam` keeps one copy awake instead, for about $4 a month. Together with the database that comes to more than the monthly budget, so it stays at 0.
- **Azure SQL** on the Basic tier, about $5 a month, which is always on and holds up to 2 GB. It used to be on the free serverless offer, but that paused after about 20 minutes without use and took close to a minute to wake, so a sync after a break waited that long. Each time the API started it woke the database too, so bots hitting the API used most of the month's free amount in the first four days of October.
- **Log Analytics** for the API's logs, capped at 0.15 GB a day so it stays inside the free 5 GB a month.
- **Blob Storage** for the photo images.
- **Key Vault** (`kvstikling` and a part that is fixed per resource group) holds the private key for push notifications, which is the only secret. Its name is the `keyVaultName` output of the deploy. It uses Azure role-based access, and the API's identity may read secrets from it. The secret is read when a revision of the API or a run of the job starts, so the Standard tier, which charges per 10,000 operations, costs next to nothing.
- **A Container Apps job**, `stikling-notify`, sends the daily notifications. It is the API's image started every 15 minutes with the argument `send-notifications`, and it runs for a few seconds and exits. That is about 2,900 runs a month, well inside Container Apps' free grant. It only exists once notifications are turned on, see below.
- **A budget** of 50 kr. a month on the subscription. The subscription bills in Danish kroner, so the budget's amount is in kroner too, and an amount written as dollars would be read as kroner. 50 kr. is about $8 a month, or $96 a year, against the $100 a year from Azure for Students. It emails support@stikling.app at half and all of the monthly amount, and runs the cost stop below.

Nothing has a password. The API signs in to the database and storage as its own managed identity (`id-stikling-api`), which is also the database's admin, so the API applies its migrations when it starts. GitHub Actions deploys as another identity (`id-stikling-deploy`) that trusts the main branch of this repository. It can change the Container App, the notification job and the environment, but not the database, the storage or who has access to them.

Sign-in goes through Clerk's production instance on `clerk.stikling.app`, with Google, Facebook and Discord set up with our own credentials from Google Cloud, Meta for Developers and Discord's developer portal. The free Clerk plan allows three social sign-ins.

## The cost stop

A budget can only send emails, and when the Azure for Students credit runs out, the whole subscription is switched off. So `infra/cost-stop.bicep` stops the API when the month's budget is spent. The app keeps working on each device, and changes wait there until the API is back. At 02:00 UTC on the 1st of each month, a second Logic App starts the API again if it isn't running, which also undoes a stop made by hand.

The stop also switches the notification job to manual, so it stops running every 15 minutes, and the start on the 1st puts it back on its schedule. The start does that every month, not only after a stop, so a hand deploy can't leave the job on manual for long. Doing it when the job is already on its schedule changes nothing.

The database can't be stopped. Basic is always on and is already the cheapest tier, and a database that has been on a paid tier can't go back to the free offer. It costs about $0.16 a day whatever happens, so a month that reaches the budget early can still end a few dollars over it. In a normal month the API costs nothing, since Azure's free amount for Container Apps covers about 200 hours of it being awake, so the stop is there for something unusual like a bot keeping it busy or a bug that makes the app sync in a loop.

Every stop and start of the API sends an email to the budget's addresses, and so does one that fails. Switching the job only sends an email when it fails. Azure works out costs several hours late, so the stop comes up to a day after the line is crossed.

While the API is stopped, a new version can't be checked after it's deployed, so the workflow's last step fails. To bring it back before the 1st, open `logic-stikling-month-start` in the portal and click Run on its overview page.

## Deploying a new version

Merging to main does it. The workflow pushes the API's image and moves the Container App to it, then the notification job too if there is one, and GitHub Pages gets the app.

## Changing the Azure resources

The Bicep isn't deployed by the workflow, since that would need the right to hand out roles. Deploy it from your own machine after `az login`:

```bash
az deployment sub create --location polandcentral --template-file infra/main.bicep --parameters infra/main.bicepparam
```

## Setting it up from nothing

This is also the way to move it to another subscription or region. For another region, change `location` in `infra/main.bicep` and delete `rg-stikling` first, since a resource group can't move and the storage and SQL names would clash with the old ones.

`githubRepository` in `infra/main.bicepparam` has the ids GitHub puts in the Actions sign-in token, as `owner@ownerId/name@repositoryId`. They come from `gh api repos/LaugeM/stikling --jq '.owner.id, .id'`. A new or renamed repository has to update it, or the API deploy fails to sign in to Azure.

1. Register the resource providers once:
   `az provider register --namespace Microsoft.App`, and the same for `Microsoft.Sql`, `Microsoft.Storage`, `Microsoft.OperationalInsights`, `Microsoft.ManagedIdentity`, `Microsoft.Logic`, `Microsoft.Insights` and `Microsoft.KeyVault`.
2. Deploy without the API's domain, and with a sample image, since the workflow hasn't pushed one yet:
   add `--parameters apiDomain='' shareDomain='' apiImage=mcr.microsoft.com/dotnet/samples:aspnetapp` to the command above. The outputs have the values for the next two steps.
3. At the DNS host for stikling.app, add a CNAME record `api` pointing at `apiDnsTarget`, and a TXT record `asuid.api` with `apiDomainVerificationId`. For the share domain, add a CNAME record `share` pointing at the same target, and a TXT record `asuid.share` with the same verification id (`shareDnsTarget` and `shareDomainVerificationId` are the same values as the API's, since it is the same container app).
4. Deploy again with `apiCertificateIssued=false shareCertificateIssued=false` and the sample image, which adds the domains and asks for their certificates. When both certificates show as succeeded under the Container Apps environment in the portal, deploy once more with only the sample image, which uses them. Set `shareCertificateIssued` to `true` in `infra/main.bicepparam` as well, as it is `false` there until then.

   On an API that is already running, only the share domain is new. Add its two DNS records first, since the deploy fails without them, then deploy with `shareCertificateIssued=false` (the default in `infra/main.bicepparam`), wait for the certificate, and deploy again with `shareCertificateIssued=true`. The `Share__Host` and `Share__BaseUrl` settings come with the same deploy.
5. In the repository settings, under Secrets and variables, then Actions, add the secrets `AZURE_CLIENT_ID` (the `deployClientId` output), `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID`. They aren't passwords, but as secrets they're hidden in the workflow's logs, which anyone can read. Until they're there, the API deploy fails and the rest of the workflow carries on.
6. Run the workflow on main. The first time, the API deploy fails because the image it just pushed is private. Make the `stikling-api` package public under Packages on GitHub, so Container Apps can pull it without a password, and run the failed job again.

From then on the command above works with the defaults.

For the app itself, the domain is set under Pages in the repository settings, with the four A records GitHub lists for an apex domain.

## Turning on notifications

The notification endpoints answer 503, and there is no job, until the server has a pair of VAPID keys. The private key is a secret, so it goes in the Key Vault by hand and never in the repository. In the commands, `<vault>` is the Key Vault's name, which starts with `kvstikling`. Get it from the deploy's output with `az deployment sub show --name main --query properties.outputs.keyVaultName.value -o tsv` (use the name `az deployment sub list -o table` shows for the deploy), or with `az keyvault list -g rg-stikling --query [].name -o tsv`.

1. Deploy the Bicep as usual. It creates the vault. `vapidPublicKey` is still empty, so there is no job yet.
2. Give yourself the right to write secrets:
   `az role assignment create --role "Key Vault Secrets Officer" --assignee "$(az ad signed-in-user show --query id -o tsv)" --assignee-principal-type User --scope "$(az keyvault show --name <vault> --query id -o tsv)"`
3. Make the keys: `npx web-push generate-vapid-keys --json`.
4. Store the private key: `az keyvault secret set --vault-name <vault> --name vapid-private-key --value <privateKey>`.
5. Put the public key in `vapidPublicKey` in `infra/main.bicepparam`, commit it, and deploy the Bicep again. That adds the secret and the settings to the API, and creates the job.
6. Check that it runs: `az containerapp job execution list --name stikling-notify --resource-group rg-stikling -o table` after 15 minutes. Then run `logic-stikling-month-start` once from the portal, which should succeed and leave the job on its schedule.

Changing the keys makes every device's subscription invalid, so people have to turn notifications on again.

Costs: the job runs about 2,900 times a month for a few seconds each, which is inside Container Apps' free grant. The Key Vault Standard tier charges per 10,000 operations, and the secret is only read when a revision of the API or a run of the job starts.

## Error reports

Errors in the app and the API go to Sentry, so problems are seen without anyone writing in. The organization is on the free Developer plan, which has 5,000 errors a month and keeps 30 days of history. Its data is stored in the EU, which can only be chosen when the organization is created.

There are two projects, one for the app and one for the API. In each project's settings, turn on "Prevent storing of IP addresses" and keep the data scrubber on.

The app's DSN goes in `src/Stikling.Web/wwwroot/appsettings.Production.json` under `Sentry:Dsn`. The API's goes in `infra/main.bicepparam` as `sentryDsn`, which is deployed by hand like any Bicep change. With a DSN left empty, that side sends nothing. DSNs are public by design, like Clerk's publishable key, so they can be in the repository.

Session tracking, tracing and replays are turned off in code on purpose. What the app leaves out of a report, and the switch for it in Settings, are on the privacy page.

## Looking at the database

Only the API's identity can sign in to the database. To look at it yourself, make your own account the admin for a while under Microsoft Entra ID on the SQL server in the portal, and set it back to `id-stikling-api` afterwards. The API can't reach the database while you are the admin.
