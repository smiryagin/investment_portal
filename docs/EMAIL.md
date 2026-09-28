# Transactional email

WiseLine Portal sends transactional email through Resend. Application code depends on the
provider-neutral `IEmailSender` and `ITransactionalEmailOutbox` contracts so a future provider
change does not affect authentication or billing use cases.

## Delivery flow

1. A user action writes a message into `communications.EmailOutbox` in the same SQL transaction as
   the related portal change.
2. `EmailOutboxWorker` atomically claims one message, then sends it through the Resend HTTPS API.
3. Every provider request carries the outbox idempotency key to prevent duplicate sends during
   retries.
4. After Resend accepts the message, HTML and text content are removed from the outbox row so reset
   and confirmation tokens are not retained.
5. Resend posts signed delivery events to `/api/webhooks/resend`. The portal verifies the raw body
   and Svix headers before writing a deduplicated record to `communications.EmailWebhookEvents`.

The initial templates cover email confirmation, password reset, and welcome messages. Subscription
and payment notifications should enqueue through the same outbox after those provider workflows are
enabled.

## Domain

The sending domain is `email.wiselinetrade.com`. SPF, DKIM, return-path, and DMARC records are
managed in Cloudflare. The default sender is:

```text
WiseLine Trade <no-reply@email.wiselinetrade.com>
```

## Configuration

Runtime configuration keys:

```text
Email__Enabled
Email__ApiKey
Email__FromAddress
Email__ReplyToAddress
Email__PublicBaseUrl
Email__WebhookSecret
Email__OutboxPollSeconds
```

GitHub Actions maps `RESEND_API_KEY` and `RESEND_WEBHOOK_SECRET` secrets plus
`RESEND_FROM_ADDRESS` and `RESEND_REPLY_TO_ADDRESS` variables into the external IIS configuration.
Use separate sending-only, domain-restricted API keys for staging and production.

## Webhook registration

Deploy the endpoint before registering the webhook. Create one Resend webhook per environment:

```text
Staging:    https://staging.wiselinetrade.com/api/webhooks/resend
Production: https://wiselinetrade.com/api/webhooks/resend
```

Subscribe to:

```text
email.sent
email.delivered
email.delivery_delayed
email.bounced
email.complained
email.failed
email.suppressed
```

Store each returned signing secret in that environment's `RESEND_WEBHOOK_SECRET`. The endpoint
rejects missing, invalid, modified, or older-than-five-minute signatures.

## Operational notes

- The worker retries only transient network, rate-limit, concurrent-idempotency, and server errors.
- Permanent provider rejections stop immediately; transient errors stop after eight attempts.
- Webhook payload bodies are not retained. The database stores only event metadata and a SHA-256
  payload fingerprint for deduplication and audit.
- IIS Data Protection keys live beside external configuration rather than in a release directory.
  The deployment script grants the app pool access and Windows DPAPI protects the key material.
- Do not log API keys, signing secrets, confirmation links, reset links, or raw webhook bodies.
