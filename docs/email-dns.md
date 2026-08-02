# Email deliverability: SPF, DKIM, DMARC, and PTR

Certificates are emailed via the `Smtp` configuration section (using MailKit) to any SMTP relay — commonly a self-hosted Postfix instance on the same VM, or another relay you control. Getting email delivered reliably to Gmail, Outlook, and similar providers has almost nothing to do with the application and everything to do with DNS and sender reputation. This guide covers the DNS records and warm-up practices needed regardless of which relay you use. Examples below use `example.org` as a placeholder — replace it with your organization's real sending domain.

## Architecture reminder

```
CertificateEngine (MailKit) --SMTP submission (587/STARTTLS)--> Postfix (local relay) --SMTP (25)--> recipient's mail server
```

`Smtp:Host`/`Smtp:Port` point at your relay (Postfix, or an upstream smart host), not directly at the recipient. Everything in this document is about making the relay's outbound mail (from Postfix to the wider internet) trusted.

> **Port 25 note:** many cloud providers block outbound port 25 by default on new VMs to fight spam. If Postfix can't connect out on port 25, request that your provider unblock it for your instance, or configure Postfix to relay through an upstream smart host over port 587/465 instead of delivering directly.

## 1. SPF (Sender Policy Framework)

SPF tells receiving mail servers which IPs are allowed to send mail claiming to be from your domain. Add a TXT record on the sending domain's apex:

```
example.org.   IN   TXT   "v=spf1 ip4:203.0.113.10 -all"
```

Replace `203.0.113.10` with your VM's public IP (or add `a`/`mx` mechanisms if more appropriate). Notes:

- Start with `~all` (soft fail) while you're setting things up and warming up the IP, then move to `-all` (hard fail) once you've confirmed mail flows correctly — `-all` tells receivers to reject anything not listed outright.
- A domain can only have **one** SPF record. If `example.org` already has a TXT record starting with `v=spf1` (e.g. for another sender), add your IP to it instead of creating a second one.

## 2. DKIM (DomainKeys Identified Mail)

DKIM signs outgoing messages with a private key so receivers can verify the message wasn't altered in transit and really came from a host authorized by the domain. With Postfix, the usual tool is OpenDKIM:

```
sudo apt install opendkim opendkim-tools
sudo mkdir -p /etc/opendkim/keys/example.org
sudo opendkim-genkey -b 2048 -d example.org -s mail -D /etc/opendkim/keys/example.org
sudo chown opendkim:opendkim /etc/opendkim/keys/example.org/mail.private
```

This produces `mail.private` (keep on the server, mode 600, owned by the `opendkim` user) and `mail.txt` (the DNS record to publish). Wire OpenDKIM into Postfix as a milter (`smtpd_milters`/`non_smtpd_milters` in `main.cf`), then publish the public key from `mail.txt` as a TXT record:

```
mail._domainkey.example.org.   IN   TXT   "v=DKIM1; h=sha256; k=rsa; p=MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...(paste the p= value from mail.txt here)"
```

The selector (`mail` above) is arbitrary but must match the `s=` value OpenDKIM signs with. You can run multiple selectors (e.g. to rotate keys) as long as each has its own TXT record.

## 3. DMARC

DMARC tells receivers what to do with mail that fails SPF/DKIM, and where to send aggregate reports. Add:

```
_dmarc.example.org.   IN   TXT   "v=DMARC1; p=quarantine; rua=mailto:dmarc-reports@example.org; pct=100; adkim=s; aspf=s"
```

- Start with `p=none` (monitor-only — nothing is rejected, you just get `rua` reports) while you confirm SPF and DKIM are both passing and aligned.
- Move to `p=quarantine` and eventually `p=reject` once reports look clean, to stop anyone from spoofing your domain.
- `adkim=s`/`aspf=s` request *strict* alignment (the visible `From:` domain must exactly match the SPF/DKIM domain) — relax to `r` (relaxed) only if you have a specific reason to (e.g. subdomain sending).

## 4. Reverse DNS (PTR) and mail server reputation

- The VM's public IP needs a **PTR record** resolving back to your mail server's hostname (e.g. `mail.example.org`). This is configured with your **hosting/cloud provider**, not in your own DNS zone, because the provider owns the IP block. Look for "reverse DNS," "PTR record," or "rDNS" in your provider's network/VM settings.
- Postfix's `myhostname` should match that PTR hostname, and ideally match the CN/SAN on the certificate Postfix presents for STARTTLS.
- Missing or mismatched PTR records are one of the most common reasons large providers (Gmail, Outlook/Microsoft) silently drop or spam-box mail from a new IP — set this up before sending any real volume.

## 5. Reputation warm-up

A brand-new IP/domain has no sending history, and mailbox providers treat unfamiliar senders with suspicion:

- Start by sending a small volume (tens, not hundreds, of messages per day) and increase gradually over 1–2 weeks rather than sending an entire event's worth of certificates in one burst on day one.
- Keep bounce and complaint rates low — an occasional bounce from a mistyped participant email is normal, but a spike in bounces looks like spam to receiving providers.
- If your NGO's sending pattern is inherently spiky (e.g., a couple thousand certificates the moment a big event's approvals are batch-processed), warm up well before the first real event, or relay through an established provider/smart host for the first few sends instead of a cold VM IP.
- Test deliverability with tools like [mail-tester.com](https://www.mail-tester.com/) and [MXToolbox](https://mxtoolbox.com/) before relying on the relay for real participant emails, and re-check occasionally with `dig txt example.org`, `dig txt mail._domainkey.example.org`, and `dig txt _dmarc.example.org`.

## Checklist

- [ ] SPF TXT record published and passing
- [ ] DKIM key generated, Postfix signing outbound mail, public key TXT record published
- [ ] DMARC TXT record published (start at `p=none`, move to `p=quarantine`/`reject` after monitoring)
- [ ] PTR record set with hosting provider, matches Postfix `myhostname`
- [ ] Outbound port 25 confirmed open (or relaying through a smart host)
- [ ] Sent a handful of test emails to `mail-tester.com` and scored well
- [ ] Warm-up plan in place before the first high-volume send
