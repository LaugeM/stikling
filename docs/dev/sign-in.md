# Signing in from the app

How the app talks to Clerk and the API, and how to test signed-in screens and syncs locally. The API's side is in `docs/dev/api.md`.

- Clerk has no Blazor library, so it is behind `wwwroot/js/account.js`, and `AccountService` is the only class that calls that file. Calls to the API go through `StiklingApi`, which adds the session token to each request.
- Clerk's scripts are only loaded when they are needed: on the sign-in page, or for syncing on a device where someone is signed in. Syncing starts once the app is on screen, never before, so it still opens offline, and a device where nobody signed in never loads Clerk.
- Signing in is only offered when `wwwroot/appsettings.{Environment}.json` has the Clerk instance and the API address. Locally that is `appsettings.Development.json`, which is kept out of the published site. The live site uses `appsettings.Production.json`, with Clerk's production instance and the hosted API.
- The API only accepts tokens from the origins in its `AppOrigins`. Locally that is `http://localhost:*`, so the dev server can run on whichever port is free.
- To test signed-in screens and real syncs, start the local API with `docker compose up --build` and use "Sign in as a test person" on the sign-in page instead of Clerk. It only shows in Development. The app signs the tokens itself with `TestSignIn:SigningKey`, which is the same in both `appsettings.Development.json` files, and the hosted API never accepts them (`Auth/TestSignIn.cs`, and a test checks it). The same name is the same person on every port, so two dev servers can sync with each other, and a new name is a new person, e.g. after deleting the account.
