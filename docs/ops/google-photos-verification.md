# Google verification for Pick from Google Photos

Stikling asks for one sensitive scope, `https://www.googleapis.com/auth/photospicker.mediaitems.readonly`, so the Google Cloud project's OAuth consent screen has to be verified before people other than the test users can use it without the "unverified app" warning. Sign-in itself (Clerk) uses the same project and is already in production.

## What the console asks for

- App name, support email and developer contact email.
- App home page: `https://stikling.app`
- Privacy policy: `https://stikling.app/privacy` (has the Google Photos section and the Limited Use sentence)
- Authorized domain: `stikling.app`, which has to be verified in Search Console under the same Google account. This is done.
- The scope, and the justification below.
- A demo video, as an unlisted YouTube link.
- Publishing status set to "In production" before submitting.

## Scope justification

Paste this in the justification field.

> Stikling (https://stikling.app) is a free app for keeping track of houseplants and cuttings. People add photos to their plants to follow how they grow. Some of those photos only exist in Google Photos, not on the phone, so we use the Google Photos Picker API to let the person pick them.
>
> We request photospicker.mediaitems.readonly only so the app can read the photos the person chooses in Google's own picker. The app creates a picking session, the person selects photos in Google Photos, and the app downloads exactly those photos and attaches them to the plant they are looking at. We can't see, list or change anything else in their library, and the Picker API doesn't offer a narrower scope.
>
> The access token is held only in memory by the open page, for at most an hour. It is never stored and never sent to our server. The photos go from Google straight to the person's device. We don't use them for anything except showing them in that person's own plant records, and we don't sell, share or use them for advertising or to train any model.
>
> Our use of information received from Google APIs adheres to the Google API Services User Data Policy, including the Limited Use requirements. The feature is off by default and the person turns it on in Settings.

## Demo video

Use an unlisted YouTube video, English, with the browser address bar visible the whole time. Under 4 minutes. It should show, in this order:

1. Open `https://stikling.app`, go to Settings and show the "Pick from Google Photos" switch off, then turn it on.
2. Open a plant and tap the Google Photos button to add a photo.
3. Google's consent screen, with the app name, the project's client ID visible in the URL, and the requested permission ("See and download the photos you pick"). Click Allow.
4. Google's picker opens. Pick one or two photos.
5. Back in Stikling, the photos being fetched and then showing on the plant.
6. Open Privacy from Settings and scroll to the Google Photos section with the Limited Use sentence.
7. Say out loud, or caption, that the token is only kept in the open page and photos never go through Stikling's server.

Record with a Google account that has a few harmless photos in it, and clear the plant afterwards.

## After submitting

Google replies by email to the contact address, often within a few days and sometimes asking for a clarification or a new video. Reply in the same thread. While it is pending, the app works for the test users listed on the consent screen, and anyone else gets the unverified app warning.
