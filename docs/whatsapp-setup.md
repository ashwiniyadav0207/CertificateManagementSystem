# WhatsApp setup (Meta Cloud API)

WhatsApp delivery talks directly to Meta's WhatsApp Cloud API over `HttpClient` — no SDK. This guide covers the Meta-side setup needed before `WhatsApp:Enabled` can be turned on: business verification, app creation, credentials, and template approval.

**Start this process early.** Meta Business verification alone can take anywhere from a few days to several weeks, and message templates need separate approval on top of that. Don't plan on turning on WhatsApp delivery the week of an event if you haven't started onboarding yet — treat email as the default channel until WhatsApp is fully approved and tested (`SendWhatsApp: false`, `EmailFallbackForWhatsApp: true` is a safe default while you wait).

## 1. Meta Business verification

1. Create or sign into a [Meta Business Account](https://business.facebook.com).
2. In **Business Settings → Security Center**, start **business verification**. You'll need legal business documents (registration/incorporation documents, proof of address, etc. — requirements vary by country).
3. This step is the main source of delay — submit it as soon as you know you'll want WhatsApp delivery, independent of everything else in this guide.

## 2. Create a Meta app and add WhatsApp

1. Go to [developers.facebook.com](https://developers.facebook.com/apps) and create a new app of type **Business**.
2. Add the **WhatsApp** product to the app.
3. In **WhatsApp → API Setup**, Meta provides a free test phone number, good for sending to up to 5 manually-verified recipient numbers. Use this for development only.
4. For production, add your organization's real WhatsApp Business phone number under **WhatsApp → API Setup → Add phone number**, and complete phone number verification (SMS/voice code).

## 3. Get `PhoneNumberId` and `AccessToken`

- **`PhoneNumberId`** — shown in **WhatsApp → API Setup** next to your (test or production) phone number. Copy it into `WhatsApp:PhoneNumberId`.
- **`AccessToken`**:
  - For quick local testing, the temporary access token shown on the same API Setup page works, but expires after 24 hours.
  - For anything long-running, go to **Business Settings → Users → System Users**, create a System User, assign it the `whatsapp_business_messaging` and `whatsapp_business_management` permissions on your app/asset, and generate a **permanent** token. Use this for `WhatsApp:AccessToken`, and treat it as a secret — set it via environment variable, never in `appsettings.json`.

## 4. Create and approve a message template

Meta requires every business-initiated message (including certificate delivery) to use a **pre-approved template** — you cannot send an arbitrary freeform message with a document attachment to someone who hasn't messaged you first within the last 24 hours.

1. Go to **WhatsApp Manager → Account tools → Message templates → Create template**.
2. **Category**: choose **Utility** — certificate delivery is a transactional notification, not marketing, and utility templates are reviewed against a lower bar and billed at a lower rate than marketing templates.
3. **Name**: must exactly match `WhatsApp:TemplateName` (default `certificate_delivery`).
4. **Language**: must exactly match `WhatsApp:TemplateLanguage` (default `en`) — double-check whether Meta's picker for your chosen language uses a bare code or a locale variant like `en_US`, and make the config match exactly what you selected.
5. **Header type**: `Document` — this is what allows the certificate PDF to be attached to the message.
6. **Body**: include two variable placeholders, matching what the engine fills in (participant name and event name). For example:

   ```
   Hi {{1}}, congratulations on completing {{2}}! Your certificate is attached.
   ```

7. Submit for review. Keep the copy plain, factual, and non-promotional — templates that read like marketing (exclamation-heavy, discount-like language, etc.) are more likely to be rejected even under the Utility category.
8. Wait for approval (ranges from minutes to a couple of days in practice) before enabling `WhatsApp:Enabled` in production — sends against an unapproved or still-pending template will fail.

## 5. Costs

Meta bills the WhatsApp Business Platform per **conversation** (a 24-hour window opened by a message, categorized as Utility, Marketing, Authentication, or Service), not per individual message — sending several messages inside the same open window to the same recipient doesn't multiply the cost. Utility conversations (which certificate delivery falls under) are priced lower than Marketing ones. Rates are set per-recipient-country and change periodically, so check Meta's current [WhatsApp Business Platform pricing](https://developers.facebook.com/docs/whatsapp/pricing) page for the countries your participants are in before budgeting — don't rely on a number baked into this document going stale.

## 6. Daily sending limits and quality rating

New WhatsApp Business senders start in a limited messaging tier (historically starting around 250 unique customers contacted per rolling 24 hours) that automatically increases over time based on usage and your phone number's **quality rating**. Quality rating is driven by recipient block/report rates and complaints about unwanted messages, and a low rating can freeze your current tier or even temporarily pause sending. To protect it:

- Only message people who actually submitted the Form/consented to be contacted — don't repurpose the number for anything else.
- Keep template wording accurate to what's actually sent (don't let the approved template drift from reality).
- Watch the phone number's quality rating in WhatsApp Manager, especially after a large batch send.

Check Meta's current documentation for exact tier thresholds, since these are adjusted from time to time.

## 7. Checklist

- [ ] Meta Business verification submitted (start this first — it's the long pole)
- [ ] Meta app created, WhatsApp product added
- [ ] Production phone number added and verified (not just the free test number)
- [ ] Permanent system-user access token generated
- [ ] `certificate_delivery` (or your chosen name) template created with a Document header and two body parameters, submitted, and **approved**
- [ ] `WhatsApp:PhoneNumberId`, `WhatsApp:AccessToken`, `WhatsApp:TemplateName`, `WhatsApp:TemplateLanguage` configured
- [ ] Sent a real end-to-end test to a personal phone before relying on it for a live event
