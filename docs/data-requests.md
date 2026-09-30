# Requests for someone's data

The privacy page tells people to write to support@stikling.app about their data. This is what to do when someone asks for a copy of it. Answer within a month, which is the deadline under the GDPR.

## Point them to the button

*Download your data* on the Account page in Settings gives a ZIP of everything the server holds about the person who is signed in: their account, collections, every record including deleted ones, settings and photos, plus what Clerk has about them. Being signed in is what proves who they are, so this is the answer to almost every request.

If they signed up with Google or Discord and lost that account, they can still sign in with the code Clerk emails to their address, as long as they can read that email.

## If they can't sign in

1. Look the account up in Clerk's dashboard, under Users, by the email address they gave.
2. Reply only to the email address on that account. If the request came from another address, don't answer it there, not even to say whether an account exists. Write to the account's own address instead and ask them to confirm the request from there.
3. Once they have confirmed from that address, make the export by hand. Copy what Clerk shows on the user's page (email addresses, name, connected accounts, sessions), and note the user id that starts with `user_`.
4. Sign in to the database as described under "Looking at the database" in [hosting.md](hosting.md), and run these with that user id:

   ```sql
   DECLARE @clerk nvarchar(450) = 'user_...';

   SELECT * FROM People WHERE ClerkUserId = @clerk;

   SELECT c.Id, c.Name, c.CreatedAt, m.Role, m.CreatedAt AS JoinedAt
   FROM Memberships m JOIN Collections c ON c.Id = m.CollectionId JOIN People p ON p.Id = m.PersonId
   WHERE p.ClerkUserId = @clerk;

   SELECT r.CollectionId, r.Kind, r.Data
   FROM Records r
   WHERE r.CollectionId IN (SELECT m.CollectionId FROM Memberships m JOIN People p ON p.Id = m.PersonId WHERE p.ClerkUserId = @clerk)
   ORDER BY r.CollectionId, r.Kind, r.Version;

   SELECT s.Data FROM PersonSettings s JOIN People p ON p.Id = s.PersonId WHERE p.ClerkUserId = @clerk;
   ```

   Set the admin back to `id-stikling-api` afterwards.
5. The photos are in the storage account's `photos` container, in a folder named by the collection id. Give your own account the Storage Blob Data Reader role on the storage account for a while, download the folder with `az storage blob download-batch --auth-mode login`, and remove the role again.
6. Put it all in a ZIP and send it only to the account's own address.
