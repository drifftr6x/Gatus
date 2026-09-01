# Microsoft Teams alert webhooks

This guide configures Gatus Kiosk to send alert notifications to a Microsoft Teams channel.

> **Important Teams platform note:** Microsoft is retiring legacy Office 365 Connectors. Use the current **Workflows / Incoming Webhook** flow in Teams rather than relying on an old connector URL. The Gatus channel currently sends a Microsoft Teams MessageCard payload to the configured webhook URL.

## Prerequisites

- A Microsoft Teams team and channel where you can add/configure workflows
- Permission to create or manage a Teams workflow
- Gatus API and admin web running
- An administrator or editor account in Gatus

## Part 1 — Create the Teams webhook

1. Open **Microsoft Teams**.
2. Open the target **team** and **channel**.
3. Open the channel menu (`...`) and choose **Workflows**. Depending on your Teams client, this may appear under **Apps** or **Connectors**.
4. Create an incoming webhook workflow. Use the template similar to:

   ```text
   Post to a channel when a Teams webhook request is received
   ```

5. Select the target team and channel.
6. Save/create the workflow.
7. Copy the generated **HTTP POST URL**.

Treat the URL like a password. Anyone who has it may be able to post to the channel. Do not commit it to Git, paste it into a ticket, or include it in screenshots.

### If Workflows is unavailable

Your Microsoft 365 administrator may have disabled Workflows or incoming webhooks. Ask the administrator to allow the Teams Workflows app and channel workflow creation. Do not use a legacy Office 365 Connector URL for a new installation unless your tenant explicitly still supports it.

## Part 2 — Add the channel in Gatus

1. Sign in to the Gatus admin console.
2. Open **Notifications**.
3. Click **Add Channel**.
4. Enter a name, for example:

   ```text
   Store Operations Teams
   ```

5. Set **Type** to **Microsoft Teams**.
6. In **Configuration (JSON)** enter the webhook URL:

   ```json
   {
     "webhookUrl": "https://your-teams-workflow-url"
   }
   ```

7. Leave **Enabled** checked.
8. Click **Save**.

The field must be valid JSON. The property name is case-sensitive in the documented configuration: `webhookUrl`.

## Part 3 — Test the webhook

1. On the Notifications page, find the Teams channel.
2. Click **Test**.
3. Confirm a test notification appears in the Teams channel.
4. If it succeeds, the UI displays a successful test result.
5. If it fails, check the API log and the troubleshooting section below.

The test message is similar to:

```text
🔔 Info: Test notification
Device: Test Device
Severity: Info
Time: <UTC time>
This is a test notification from Sentinel Kiosk.
```

## Part 4 — Receive real alerts

The notification service sends notifications to every enabled channel when the alert evaluator raises an alert.

To verify the complete path:

1. Confirm the Teams channel is enabled.
2. Open **Alerts** and confirm alert rules are enabled.
3. Trigger a safe development/test condition, such as a test alert supported by your environment.
4. Confirm the alert appears in **Alerts**.
5. Confirm the corresponding message appears in Teams.
6. Review the API logs if the alert appears in Gatus but not in Teams.

A channel must be enabled for real alerts. The notification service does not send to disabled channels.

## Alert message format

Gatus sends a MessageCard-style JSON payload containing:

- Alert severity
- Alert title
- Alert message
- Device name
- UTC raised timestamp
- Severity-based theme color

The current implementation supports channel type `teams` and reads `webhookUrl` from `ConfigJson`.

## Troubleshooting

### Test reports success but no Teams message appears

- Verify the workflow points to the intended team and channel.
- Confirm the copied URL is the workflow HTTP POST URL, not a Teams channel URL.
- Check the workflow run history in Teams/Power Automate.
- Generate a new webhook URL if the old one was revoked.
- Confirm the API machine can make outbound HTTPS requests.

### Test returns HTTP 400 or 403

- Confirm the webhook URL was copied completely.
- Check that the workflow is enabled.
- Confirm the workflow trigger is the incoming Teams webhook trigger.
- Check tenant policy or channel permissions.
- Recreate the workflow if it was created from a retired connector.

### Gatus says the JSON is invalid

Use exactly this shape and double quotes:

```json
{
  "webhookUrl": "https://..."
}
```

Do not add comments or trailing commas.

### Real alerts do not send, but Test works

- Confirm the channel is enabled.
- Confirm the alert evaluator is running in the API process.
- Confirm the alert is newly raised; deduplication may prevent repeated notifications for the same active alert.
- Check API logs for `Failed to send notification via channel`.

### Where to look in logs

From the repository root, API logs are normally under:

```text
apps/api-server/logs/log-YYYYMMDD.json
apps/api-server/logs/user-actions-YYYYMMDD.json
```

Use the admin console’s **Logs** page, select **Server Logs**, and search for:

```text
Teams notification sent
Failed to send notification via channel
```

## Security and operations

- Store webhook URLs only in the database/configuration store with restricted administrator access.
- Rotate the Teams webhook if it is exposed.
- Do not place webhook URLs in source control or `.env.example`.
- Restrict who can create/edit notification channels using the server-side RBAC policies.
- Review outbound webhook behavior before exposing the API to the public Internet. The current implementation performs an outbound HTTP request to the configured URL; production deployments should add URL validation, egress controls, secret masking, and audit coverage.
