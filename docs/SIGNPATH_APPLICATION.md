# Open-source signing application checklist

Preparation is complete locally; repository publication and provider approval are separate steps.

1. Create a separate public repository containing ONLY this package. Do not push the parent workspace or private repository history.
2. Confirm ownership of the included Client code/assets and retain upstream notices. Publish the MIT license and source-build instructions. The administrator program remains outside this repository.
3. Fill actual GitHub maintainer/reviewer/approver identities in CODE_SIGNING_POLICY.md; enable MFA and branch protection. Set the homepage/support contact and production privacy/retention information. Link Code signing policy and Privacy from the download page.
4. Push the source; allow the Client source build workflow to pass. Publish an honestly labeled unsigned source-build release. Verify its correspondence to source.
5. Apply at https://signpath.org/apply.html. Provide the Client repository/homepage, functionality, source license, build workflow and source/binary releases. Disclose foreground remote control, code-based authorization, USB driver preparation and the GPL upstream payload. This project is new and small; provider reputation/eligibility review may reject or defer it.
6. Ask the reviewer whether an open host interoperating with a separate closed receiver meets their policy; do not claim automatic acceptance. Offer only Client scope. Do not submit or disclose private receiver implementation or server administration credentials.
7. After acceptance, configure the trusted build origin, Client-only artifact restrictions and human signing approval in the provider account. Record assigned project/policy slugs and the organization ID as repository variables. Store SIGNPATH_API_TOKEN in a protected GitHub Actions secret.
8. Variables: ENABLE_SIGNPATH=true, SIGNPATH_ORGANIZATION_ID, SIGNPATH_PROJECT_SLUG, SIGNPATH_SIGNING_POLICY_SLUG. Keep ENABLE_SIGNPATH unset until the provider has accepted and configured the project. Never put API tokens in the public ZIP or messages.
9. Create a client-v version tag after review; the workflow uploads only DaktiloClient.exe and requests signing. A human approves it. Validate Authenticode and publish the returned signed Client without modifying its bytes afterward.
10. Add the provider attribution only once actually accepted. Test downloaded files on a clean Windows machine. No guarantee is made that signing removes every SmartScreen prompt immediately.

The GitHub workflow and provider account integration have not been run online; local compilation and tests are documented separately. The provider's current terms and controls prevail over this preparation checklist.
