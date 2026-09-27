# Vendor SDK License Policy

Last reviewed: 2026-08-04 KST

## Purpose

Raw Buffer Visualizer must not create a vendor-license or trademark risk merely to add a direct camera-SDK convenience adapter. Technical compatibility evidence is not legal permission to download, test, integrate, distribute, or advertise support for a vendor SDK.

This document is a conservative project release gate, not legal advice. Only the vendor's written authorization for this project or review by qualified counsel can clear an ambiguous proprietary-SDK use.

## Non-Negotiable Gate

A proprietary SDK must not be downloaded for project testing, and a vendor-named direct adapter must not be implemented, enabled in a release, described as supported, included in Marketplace copy, or distributed from a public branch until all applicable conditions pass:

1. The developer is eligible to accept the SDK license. A license that excludes consumers does not cover this individual project unless the vendor confirms otherwise in writing.
2. The permitted purpose covers development and testing of a debugger visualizer. Camera-only, owned-hardware-only, or image-origin restrictions require written vendor clarification when the project does not meet them.
3. The vendor permits the exact public interoperability technique, including use of documented managed type/member names, debugger visualizer registration, and any simulator or emulator used for testing.
4. The vendor permits a free MIT-licensed extension to identify compatibility using the vendor and SDK names. Logos are never used without separate permission.
5. No vendor SDK binary, driver, header, documentation, sample, or license file is committed, packaged, mirrored, or redistributed unless its license expressly permits that exact distribution and counsel approves it.
6. The project records the license URL/text, review date, applicable SDK version, written authorization, test artifact identity, and release-copy wording before publication.

If any condition is unknown, the result is **Blocked**, not "probably permitted".

## Safe Default

`RawBufferView` and `RawBufferSnapshot` remain the vendor-neutral integration path. They accept buffers and metadata supplied by the user's own application and do not require Raw Buffer Visualizer to install or redistribute a camera SDK. Generic structural recognition must not be marketed as vendor certification.
