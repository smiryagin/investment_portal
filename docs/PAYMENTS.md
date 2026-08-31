# Payments

WiseLine supports hosted Stripe Checkout for cards and PayPal Subscriptions. Provider secrets are environment-specific and are written to the external IIS configuration file during deployment. They are never committed to Git.

## Stripe

Create a recurring Stripe Price for **$10 USD monthly**. Configure Checkout to use the price ID. The API requests subscription mode, `payment_method_collection=always`, and a 14-day trial.

Create a webhook endpoint for:

```text
https://wiselinetrade.com/api/payments/webhooks/stripe
```

Subscribe at minimum to `customer.subscription.created`, `customer.subscription.updated`, and `customer.subscription.deleted`. Store the signing secret as the GitHub environment secret `STRIPE_WEBHOOK_SECRET`. Use Stripe test credentials in staging and live credentials only in production.

## PayPal

Create a PayPal product and $10 monthly subscription plan. The plan must include a 14-day free trial followed by regular monthly billing. Set the staging API base URL to `https://api-m.sandbox.paypal.com` and production to `https://api-m.paypal.com`.

Create a webhook endpoint for:

```text
https://wiselinetrade.com/api/payments/webhooks/paypal
```

Subscribe to subscription activated, updated, suspended, canceled, expired, and payment-failed events. Save the PayPal webhook ID; every notification is verified with PayPal before it changes subscription state.

## Important behavior

- Registration alone does not start a trial.
- A verified provider webhook is required before entitlement begins.
- Checkout success redirects are informational and are not payment proof.
- Provider event IDs are unique in the portal database, making retries idempotent.
- Promotion codes extend only an active trial and are separately limited to one redemption per user/code.

## Public-launch guard

The current database constraint prevents a portal user from starting more than one trial. Before public registration is enabled, add provider payment-instrument fingerprint resolution and persist a one-way fingerprint key so the same card or PayPal funding source cannot claim trials through multiple portal accounts. Do not store card numbers, CVV values, or raw payment credentials in WiseLine.
