# ZKTeco ADMS Gateway

A multi-tenant HTTP gateway for **ZKTeco PUSH protocol devices** (ZAM70 platform, SenseFace series, iClock series, etc.).

Devices push attendance data **to this gateway**. The gateway identifies which client the device belongs to and forwards the records to the correct client application — preserving your existing `SaveHistory` API contract.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         YOUR NETWORK                                 │
│                                                                      │
│  [SenseFace 2A]  ──┐                                                 │
│  SN: AABBCC1122    │   HTTP POST /iclock/cdata                       │
│                    ▼                                                  │
│  [SenseFace 2A]  ──►  ┌──────────────────────────┐                  │
│  SN: AABBCC4455    │  │   ZKTeco ADMS Gateway    │                  │
│                    │  │   (this app, port 8080)  │                  │
│  [iClock 990]    ──┘  │                          │                  │
│  SN: DDEEFF7788       │  Lookup SN → Client       │                  │
│                       │  Forward to client API    │                  │
│                       └──────────┬───────────────┘                  │
│                                  │                                   │
│              ┌───────────────────┼────────────────────┐             │
│              ▼                   ▼                    ▼             │
│   [Client A App]       [Client B App]       [Client C App]          │
│   192.168.1.100        10.0.0.50            gamma.example.com        │
│   /api/HrPayroll/      /api/HrPayroll/      /api/HrPayroll/          │
│   SaveHistory          SaveHistory          SaveHistory              │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Quick Start

### 1. Prerequisites
- .NET 8 SDK
- Windows / Linux server reachable by your ZKTeco devices

### 2. Clone / extract this project

### 3. Configure appsettings.json

Edit `appsettings.json` and map each device serial number to the right client:

```json
{
  "Urls": "http://0.0.0.0:8080",
  "GatewaySettings": {
    "AdminKey": "your-secret-admin-key"
  },
  "GatewayConfig": {
    "Clients": [
      {
        "ClientId": "CLIENT_A",
        "ClientName": "Acme Corporation",
        "ForwardUrl": "http://192.168.1.100/api/HrPayroll/SaveHistory",
        "ApiKey": "optional-bearer-token",
        "DeviceSerialNumbers": [
          "AABBCC112233",
          "AABBCC445566"
        ]
      },
      {
        "ClientId": "CLIENT_B",
        "ClientName": "Beta Industries",
        "ForwardUrl": "http://10.0.0.50/api/HrPayroll/SaveHistory",
        "ApiKey": "",
        "DeviceSerialNumbers": [
          "DDEEFF778899"
        ]
      }
    ]
  }
}
```

> **How to find your device's serial number:**
> On the device: Menu → System → Device Information → Serial Number.
> Or check the gateway logs after the device registers — it appears as `Device XXXXXX registered`.

### 4. Build and run

```bash
cd ZKTecoGateway
dotnet run
```

Or publish for production:

```bash
dotnet publish -c Release -o ./publish
./publish/ZKTecoGateway
```

### 5. Configure each ZKTeco device

On the device (SenseFace 2A):
```
Menu → Communication → Cloud Server / ADMS Settings
  Server Address : <IP of this gateway server>
  Server Port    : 8080
  Enable HTTPS   : Off
  Domain Name    : (leave blank unless using a domain)
```

The device will connect and start pushing data automatically.

---

## Endpoints

### Device endpoints (called by ZKTeco devices)

| Method | Path | Purpose |
|--------|------|---------|
| GET/POST | `/iclock/cdata` | Device registration + attendance data push |
| GET | `/iclock/getrequest` | Device polls for pending commands |
| POST | `/iclock/devicecmd` | Device acknowledges command execution |
| GET/POST | `/iclock/test` | Connectivity test |

### Admin endpoints (for you)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/admin/status` | All devices, online status, recent forward logs |
| GET | `/admin/clients` | Configured clients and device counts |

Secure admin endpoints with `X-Admin-Key: your-secret-admin-key` header.

```bash
curl http://your-gateway:8080/admin/status -H "X-Admin-Key: your-secret-admin-key"
```

---

## What the gateway sends to your client apps

The same `List<HistoryViewModel>` JSON your original pull code was already sending. **No changes needed to your existing `SaveHistory` endpoint.** The only addition is the `SN` field (device serial number) is now populated.

```json
[
  {
    "empId": "1001",
    "userId": 1001,
    "evntDate": "2024-06-08",
    "eventDate": "2024-06-08T09:32:10",
    "evntTime": "09:32:10",
    "eventTime": "09:32:10",
    "chckType": "0",
    "checkType": "Check-In",
    "verifyCode": 1,
    "logId": "1",
    "sn": "AABBCC112233",
    "machineCode": "0",
    "workCode": "0",
    "status": "Check-In",
    "remarks": ""
  }
]
```

---

## Adding a New Client / Device

1. Get the device serial number (from device menu or gateway logs).
2. Add an entry to `GatewayConfig.Clients` in `appsettings.json`.
   - If client already exists, just add the SN to `DeviceSerialNumbers`.
3. Restart the gateway (or implement hot-reload if needed).
4. Configure the device's ADMS settings to point to the gateway.

That's it. No code changes required.

---

## Deployment as a Windows Service

```bash
dotnet publish -c Release -o C:\ZKTecoGateway
sc create ZKTecoGateway binPath="C:\ZKTecoGateway\ZKTecoGateway.exe" start=auto
sc start ZKTecoGateway
```

Or on Linux with systemd:

```ini
[Unit]
Description=ZKTeco ADMS Gateway
After=network.target

[Service]
WorkingDirectory=/opt/zktecogateway
ExecStart=/opt/zktecogateway/ZKTecoGateway
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Device doesn't connect | Wrong IP/port in device ADMS config | Double-check device → Menu → Communication → ADMS |
| "Device XXXXX not mapped" in logs | SN not in appsettings.json | Add SN to the correct client's `DeviceSerialNumbers` |
| Gateway receives data but client app returns error | Client app URL wrong, or app down | Check `ForwardUrl` in config; check client app logs |
| No records arriving | Device Realtime=0 or TransInterval too high | Gateway's registration response sets `Realtime=1` automatically |
| Old devices still work? | Yes — keep running the old Standalone SDK code for legacy devices | Old CZKEM devices → old pull code. New ZAM70 devices → this gateway |

---

## ChckType / Status Values

| Value | Meaning |
|-------|---------|
| 0 | Check-In |
| 1 | Check-Out |
| 2 | Break-Out |
| 3 | Break-In |
| 4 | Overtime-In |
| 5 | Overtime-Out |
