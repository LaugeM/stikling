# Hosting

The app is static files on GitHub Pages at https://stikling.app. The sync API runs on Azure, described in Bicep in `infra/`:

- **Container Apps** runs the API from the image the workflow pushes to `ghcr.io/laugem/stikling-api`. It sleeps when nobody uses it, so the first request after a quiet spell waits a few seconds. `apiMinReplicas = 1` in `infra/main.bicepparam` keeps one copy awake instead, for a few dollars a month.
- **Azure SQL** on the free offer. The database pauses after an hour without use and takes up to a minute to wake. If the month's free amount runs out, it pauses until the next month instead of costing money.
- **Blob Storage** for the photo images.
- **A budget** on the subscription that emails support@stikling.app at half and all of the monthly amount.

Nothing has a password. The API signs in to the database and storage as its own managed identity (`id-stikling-api`), which is also the database's admin, so the API applies its migrations when it starts. GitHub Actions deploys as another identity (`id-stikling-deploy`) that trusts the main branch of this repository. It can change the Container App and its environment, but not the database, the storage or who has access to them.

Sign-in goes through Clerk's production instance on `clerk.stikling.app`, with Google set up with our own Google Cloud credentials.

## Deploying a new version

Merging to main does it. The workflow pushes the API's image and moves the Container App to it, and GitHub Pages gets the app.

## Changing the Azure resources

The Bicep isn't deployed by the workflow, since that would need the right to hand out roles. Deploy it from your own machine after `az login`:

```bash
az deployment sub create --location swedencentral --template-file infra/main.bicep --parameters infra/main.bicepparam
```

## Setting it up from nothing

This is also the way to move it to another subscription.

1. Register the resource providers once:
   `az provider register --namespace Microsoft.App`, and the same for `Microsoft.Sql`, `Microsoft.Storage`, `Microsoft.OperationalInsights` and `Microsoft.ManagedIdentity`.
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
