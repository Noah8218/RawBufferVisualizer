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

## Current Vendor Status

| Vendor SDK | Official term that matters | Project status |
| --- | --- | --- |
| Basler pylon | The current EULA permits an individual or entity to accept it, but limits permitted use to operating Basler camera products. Its derivative-distribution terms also restrict use of Basler names/trademarks for marketing and include notice, indemnity, record, and audit obligations. | **Blocked for release and support claims.** Existing `1.0.53` work is engineering evidence only. Written authorization and legal review are required before distribution. |
| Allied Vision Vimba X | The free-software terms state that the offer is not directed to consumers and no usage rights are granted to consumers. Intended use is tied to Allied Vision/TKH-supported hardware and software. | **Blocked.** Do not download, install, test, implement, or advertise a direct adapter without written authorization for this individual project. |
| IDS peak | The official terms state that the software is not offered to consumers and grant integration/distribution rights only for products operating with IDS cameras. | **Blocked.** Do not download, install, test, implement, or advertise a direct adapter without written authorization for this individual project. |
| Teledyne FLIR Spinnaker | The EULA grants use only with supported FLIR cameras owned by the licensee and images derived from those cameras. | **Blocked without owned qualifying hardware or written authorization.** Do not use a no-camera SDK installation as release evidence. |
| Other proprietary camera, frame-grabber, transport-board, and imaging-board SDKs | Not yet reviewed for the exact developer, purpose, hardware, redistribution, and trademark conditions. | **Blocked by default.** Review the current official license before any download or implementation. If the vendor does not offer applicable rights, Raw Buffer Visualizer does not provide that direct integration. |

The public Marketplace `1.0.52` package predates the direct Basler adapter and is not changed by this hold. The current working tree removes the Basler direct adapter; pushed branch commit `c35074b` still contains the historical engineering experiment until an explicitly authorized commit/push updates it. Neither state is a released or legally cleared support claim.

## Written Authorization Checklist

The vendor response must explicitly cover all of the following:

- Noah Choi acting as an individual independent developer without a camera;
- the open-source MIT project at `https://github.com/Noah8218/RawBufferVisualizer`;
- local download and installation of the official SDK;
- public API/assembly contract inspection and official simulator testing, if available;
- development and distribution of a debugger visualizer that does not control a camera;
- use of the vendor and SDK names only to state factual compatibility;
- debugger registration against documented managed type names;
- no redistribution of vendor SDK binaries, drivers, headers, documentation, or samples;
- any required notices, disclaimers, hardware restrictions, or support wording.

An answer that only provides a download link or general support instructions does not clear this gate.

## Inquiry Template

```text
Subject: Written SDK permission request for Raw Buffer Visualizer

Hello,

My name is Noah Choi. I am an individual independent developer of the free MIT-licensed project Raw Buffer Visualizer:
https://github.com/Noah8218/RawBufferVisualizer

The project is a Visual Studio debugger extension that inspects already-acquired 2D image buffers. It does not discover or control cameras and it does not redistribute vendor SDK binaries, drivers, headers, documentation, or samples.

I do not own one of your cameras. Please confirm in writing whether I may:
1. download and install the official SDK as an individual consumer;
2. inspect documented public .NET API/assembly contracts and use the official simulator, if available;
3. develop and distribute an MIT-licensed debugger adapter registered against documented managed type names;
4. use your company and SDK names only to describe factual compatibility;
5. perform those activities without redistributing your SDK.

If permitted, please state the applicable license version, required notices/disclaimers, hardware restrictions, and approved wording for a compatibility statement.

Thank you,
Noah Choi
dhqtlzm12@naver.com
```

## Official Terms Reviewed

- Basler pylon EULA: https://docs.baslerweb.com/pylon-end-user-license-agreement
- Allied Vision Vimba X terms: https://www.alliedvision.com/assets/documents/products/software/Vimba_X/Terms_and_Conditions_VimbaX_Downloads-EN.pdf
- IDS peak license terms: https://en.ids-imaging.com/download-peak.html?os=windows&version=win10
- Teledyne FLIR Spinnaker EULA: https://intecore.teledynevisionsolutions.com/globalassets/support/iis/knowledge-base/flir-spinnaker-sdk-eula-2018.pdf
