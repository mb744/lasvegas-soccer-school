# App Store Connect — App Privacy declarations

Answers to paste into the **App Privacy** section of App Store Connect for the LV Soccer School mobile app. Based on what the code actually collects; keep this file in sync with the codebase whenever mobile data collection changes.

## Meta

- **Privacy policy URL:** `https://registration.lasvegassoccerschool.org/privacy`
- **Data collection contact email:** *(use your admin email; ASC needs a real address)*

## Data types collected

Answer **Yes** to "Does this app collect data?" then declare the following. Each item lists the ASC category, exact sub-type, purpose, "Linked to user identity" and "Used for tracking" answers.

### 1. Email Address

- **Category:** Contact Info → **Email Address**
- **Purpose:** App Functionality
- **Linked to the user's identity:** **Yes**
- **Used for tracking purposes:** **No**
- Why: The user signs in with their email address. It's tied to their LVSS account permanently.

### 2. Name

- **Category:** Contact Info → **Name**
- **Purpose:** App Functionality
- **Linked to the user's identity:** **Yes**
- **Used for tracking purposes:** **No**
- Why: The parent's first/last name comes from their `ParentAccount`, is snapshotted onto each chat membership as `DisplayName`, and shown to other chat members alongside their messages.

### 3. User ID

- **Category:** Identifiers → **User ID**
- **Purpose:** App Functionality
- **Linked to the user's identity:** **Yes**
- **Used for tracking purposes:** **No**
- Why: Each request is authenticated with a JWT that carries the internal user id (the `AspNetUsers.Id`). Server persists refresh tokens tied to that id.

### 4. Device ID

- **Category:** Identifiers → **Device ID**
- **Purpose:** App Functionality
- **Linked to the user's identity:** **Yes**
- **Used for tracking purposes:** **No**
- Why: When the user grants notification permission, the app registers an Expo Push Token (a per-device identifier) with the backend so the attendance-reminder job and chat notifications can push to their phone. Tokens Expo reports as `DeviceNotRegistered` are pruned.

### 5. Other User Content

- **Category:** User Content → **Other User Content**
- **Purpose:** App Functionality
- **Linked to the user's identity:** **Yes**
- **Used for tracking purposes:** **No**
- Why: Chat messages the user sends into a `ChatGroup` are persisted on the server so history survives disconnects and so other group members can read them.

## Data types NOT collected

Explicitly answering "No" for these keeps the App Privacy card accurate:

- Financial Info (no in-app purchase / payment collection in this app)
- Location (no GPS or coarse-location APIs used)
- Health & Fitness
- Sensitive Info
- Contacts (never reads the phone's contact book)
- Browsing History / Search History
- Purchases
- Usage Data (no in-app analytics SDK)
- Diagnostics (no crash reporter SDK)

## Tracking

The **App Tracking Transparency (ATT)** prompt is **not required** because:

- No data is used for tracking as ASC defines it (linking user activity to third-party data for advertising or measurement, or sharing with data brokers).
- No third-party ad SDKs are integrated.
- No analytics SDKs that share identifiers with third parties.

Answer **No** to the ATT question. Do NOT add `NSUserTrackingUsageDescription` to `Info.plist` unless that changes.

## Notes for the reviewer

- **Password collection:** The app transmits the user's password to the backend at login for authentication only. It is never stored on the device beyond the in-memory value during sign-in. Per Apple's guidance, credentials transmitted solely for authentication are not required to be declared in App Privacy.
- **Chat messages between minors:** N/A — the app is for parents (users have a `ParentAccount`). No child accounts, no direct child-to-child messaging.
- **Content moderation:** Chat groups are admin-created and admin-moderated (see `ChatAdminController` on the backend). Members can be removed by admins at any time.

## When to update this file

Anytime any of the following change, re-review the declarations and update ASC on the next release:

- New data collected by the app (new form fields, new SDKs, new permissions).
- Removal or expansion of existing data usage.
- Addition of any third-party service (analytics, crash reporting, ads, deep linking that sends data to a third party).
- Change to the retention policy or scope of use of any collected data.
