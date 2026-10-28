Sentry browser SDK 10.76.2 (MIT), the errors-only `bundle.min.js` from Sentry's CDN: https://browser.sentry-cdn.com/10.76.2/bundle.min.js. It has no tracing, replay or feedback. `js/errors.js` starts it and decides what it sends. It reports errors from the browser side of the app, and the .NET side has its own SDK.

SHA-384, as Sentry's release registry lists it for this version (https://release-registry.services.sentry.io/sdks/sentry.javascript.browser/10.76.2):

    sha384-UxIR36IcGoGUfLoGUusrMIf62txfGSt8+2nzkKL3PMw3TFPWZ9JRczoq8o2AfY0e

To update, download `bundle.min.js` for the new version from the same address, check it against the hash the registry lists for that version, replace the LICENSE with the one from the getsentry/sentry-javascript repo at that tag, and change the version and hash here. Then check that the integration and option names `js/errors.js` uses still exist in that version.
