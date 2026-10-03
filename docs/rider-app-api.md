# Rider app + admin dispatch — API contract

Branch `feature/delivery-rider-app`. JSON is camelCase. Enums are sent and returned as **ints**
unless noted. Errors are HTTP 400 with `{ "statusCode", "errorMessage", "additioanlData" }`
(the misspelling is in the existing `ProblemDetail`). Dates are UTC ISO-8601.

## Concepts

- **Legs.** Each rented vehicle has two trips. `DeliveryLeg`: `1 = Delivery` (merchant → customer),
  `2 = Return` (customer → merchant). Each leg can have a different rider.
- **Steps.** `HandoverStep`: `1 ReceivedFromOwner`, `2 DeliveredToCustomer` (delivery leg),
  `3 ReceivedFromCustomer`, `4 DeliveredToOwner` (return leg).
- **Photos.** Every step needs exactly 4 photos. `HandoverImagePosition`: `1 Front, 2 Back, 3 Left, 4 Right`.
- **Money.**
  - Riders **never pay merchants** and hold **no cash float**. The company settles merchants.
  - On a **cash** order, the delivery-leg rider who delivers the first vehicle collects `OrderTotal`
    from the customer. That amount is a debt on him until it is remitted.
  - Commission per leg = `vehicle DeliveryFee × city leg percent / 100`, snapshotted when the leg is assigned.
    - Delivery-leg commission is credited on `DeliveredToCustomer`.
    - Return-leg commission is credited on `DeliveredToOwner`.
- **Shifts.**
  - Admin creates shifts per city: name, `HH:mm` start and end in **Egypt local time** (may cross midnight),
    and days bitmask with `Sunday = 1, Monday = 2, … Saturday = 64, all = 127`. Admin then adds riders.
  - A rider can switch **online only while one of his shifts is running**. He is shown offline once it ends.
  - A toggle left on from an earlier shift does not count; the rider has to switch on again.

---

## Rider app — `Authorization: Bearer <jwt>` with role **Delivery**

Login is the existing `POST /api/auth/Login` `{ userName, password, role: 4 }`. The response contains
`token`, `refreshToken`, `deliveryId`. Refresh with `POST /api/auth/RefreshToken { refreshToken }`.

### Profile, devices, shift
| Method | Path | Body / query | Returns |
|---|---|---|---|
| GET | `/api/delivery/DeliveryProfile/me` | | `RiderProfileDto` |
| POST | `/api/delivery/DeliveryProfile/Devices` | `{ andriodDevice?, iosDevice? }` (FCM token; field name is misspelled on purpose, same as Customer) | `true` |
| DELETE | `/api/delivery/DeliveryProfile/Devices?token=…` | | `true` |
| GET | `/api/delivery/DeliveryProfile/Shift` | | `RiderShiftStatusDto` |
| POST | `/api/delivery/DeliveryProfile/Online` | `{ isOnline: bool }` | `RiderShiftStatusDto`, or 400 "You can go online only during your shift" |

```jsonc
// RiderProfileDto
{ "deliveryId": 12, "fullName": "Ahmed Ali", "userName": "ahmed", "mobileNumber": "+20…", "email": null,
  "personalImage": "/uploads/deliveries/x.jpg", "cityId": 1, "cityName": "Hurghada", "zoneId": 4, "zoneName": "El Dahar" }

// RiderShiftStatusDto
{ "isOnline": true,            // effective: toggle on AND inside a running shift
  "onlineToggle": true,
  "isInShift": true,
  "canGoOnline": true,
  "currentShift": { "shiftId": 3, "name": "Morning", "startTime": "08:00", "endTime": "16:00", "daysOfWeekMask": 127 },
  "currentShiftEndsAt": "2026-10-03T14:00:00Z",
  "nextShift": null, "nextShiftStartsAt": null,
  "shifts": [ /* all the rider's shifts, same shape as currentShift */ ] }
```

### Orders
| Method | Path | Notes |
|---|---|---|
| GET | `/api/delivery/DeliveryOrder?tab=Pickup|Return|Completed&pageNumber=1&pageSize=20` | `tab` is sent by **name**. Returns `PagedResult<RiderOrderSummaryDto>`: `{ items, totalCount, pageNumber, pageSize, totalPages, hasPreviousPage, hasNextPage }` |
| GET | `/api/delivery/DeliveryOrder/{orderId}` | `RiderOrderDetailDto` |
| POST | `/api/delivery/DeliveryOrder/UploadImage` | multipart, field **`file`** (≤ 5 MB). Returns `{ "url": "/uploads/order-vehicles/<guid>.jpg" }` |
| POST | `/api/delivery/DeliveryOrder/{orderId}/vehicles/{vehicleId}/ReceivedFromOwner` | step body below |
| POST | `…/DeliveredToCustomer` | step body below |
| POST | `…/ReceivedFromCustomer` | step body below |
| POST | `…/DeliveredToOwner` | step body below |
| POST | `…/NotReceivedByCustomer` | `{ reason, faultParty }`, where `FaultParty` is `1 Customer, 2 Merchant, 3 Delivery, 4 Company`; delivery-leg rider only |

Tabs:
- **Pickup:** I hold a delivery leg that is not finished.
- **Return:** no delivery leg is left for me, and I hold an unfinished return leg. This includes a return
  that is waiting for another rider to deliver first; it shows with `nextAction: "None"`.
- **Completed:** all my legs on the order are done, or the order is completed or cancelled.

Step body:
```jsonc
{ "images": [ { "position": 1, "url": "/uploads/order-vehicles/a.jpg" },
              { "position": 2, "url": "…" }, { "position": 3, "url": "…" }, { "position": 4, "url": "…" } ],
  "amountConfirmed": 950.00,   // DeliveredToCustomer on a cash order: must equal nextStepMoney.amount; else 0
  "note": null }
```
Rules:
- 4 distinct positions.
- URLs must come from `UploadImage`.
- The rider must hold the leg the step belongs to, else 400 "This step is assigned to another rider".
- Steps run in order.
- Re-sending a step that is already done returns `true`, so it is safe to retry.

```jsonc
// RiderOrderSummaryDto
{ "orderId": 1045, "orderCode": "YS-1045", "orderState": 4, "isUrgent": true,
  "customerName": "John Smith", "hotelName": "Steigenberger", "destinationZoneName": "Safaga Road",
  "reservationDateFrom": "…", "reservationDateTo": "…", "myVehiclesCount": 2,
  "nextAction": "ReceiveFromOwner",           // ReceiveFromOwner|DeliverToCustomer|ReceiveFromCustomer|DeliverToOwner|None
  "collectFromCustomer": 950.00, "isPaidOnline": false, "myDeliveryFeeShare": 120.00 }

// RiderOrderDetailDto
{ "orderId": 1045, "orderCode": "YS-1045", "orderState": 4, "isUrgent": true, "notes": "Room 214",
  "customer": { "name", "mobileNumber", "hotelName", "hotelAddress", "hotelPhone", "destinationZoneName" },
  "reservationDateFrom": "…", "reservationDateTo": "…",
  "vehicles": [ {
      "vehicleId": 77, "vehicleCode": "SC-077", "vehicleName": "Vespa", "model": "2023", "color": "Red", "imageUrl": "…",
      "merchant": { "merchantId": 5, "merchantName": "Hurghada Rent", "mobileNumber": "+20…", "zoneName": "El Dahar", "address": "El Dahar" },
      "legs": { "delivery": { "deliveryId": 12, "deliveryName": "Ahmed Ali", "isMine": true },
                "return":   { "deliveryId": 31, "deliveryName": "Mazen Adel", "isMine": false } },   // null when not assigned
      "steps": { "receivedFromOwner":    { "done": true, "at": "…", "images": ["…","…","…","…"], "byDeliveryName": "Ahmed Ali" },
                 "deliveredToCustomer":  { "done": false, "at": null, "images": [], "byDeliveryName": null },
                 "receivedFromCustomer": { … }, "deliveredToOwner": { … } },
      "nextAction": "DeliverToCustomer",       // only a step on MY leg that I can do now, else "None"
      "nextStepMoney": { "type": "CollectFromCustomer", "amount": 950.00 },   // or { "type": "None", "amount": 0 }
      "deliveryFailed": false, "deliveryFailureReason": null } ],
  "money": { "paymentMethod": 0, "isPaidOnline": false, "currencyCode": "EGP",
             "collectFromCustomer": 950.00,   // what I collect / collected on this order, else 0
             "collectFromCustomerBreakdown": { "rental", "deliveryFees", "serviceFees", "urgentFees", "tieredDiscount", "previousDebt" },
             "myDeliveryFeeShare": 120.00 } }
```
Image URLs are relative; prefix them with the API base URL.

### Wallet
| Method | Path | Returns |
|---|---|---|
| GET | `/api/delivery/DeliveryWallet/Summary` | `{ cashCollected, cashRemitted, cashDebt, commissionEarned, commissionPaid, deductions, commissionDue, balance, currencyCode }` — `deductions` = fault clawbacks; `commissionDue = earned − paid − deductions`; `balance` = all credits − all debits |
| GET | `/api/delivery/DeliveryWallet/Journals?pageNumber&pageSize` | `PagedResult` of `{ id, orderId, orderCode, entryKind, amount, note, createdDate }`, where `amount` is positive in the rider's favour and negative against |

`entryKind` (int): `4 DeliveryFeeAccrued` (commission +), `5 CashCollectedFromCustomer` (−),
`7 DeliveryRemittanceToCompany` (+), `9 DeliveryPaidByCompany` (−). Older ledgers may also contain other kinds.

### Notifications
| Method | Path |
|---|---|
| GET | `/api/delivery/DeliveryNotification?pageNumber&pageSize` → `PagedResult` of `{ id, title, body, orderId, isRead, createdDate }` |
| GET | `/api/delivery/DeliveryNotification/UnreadCount` → int |
| POST | `/api/delivery/DeliveryNotification/{id}/MarkAsRead` |
| POST | `/api/delivery/DeliveryNotification/MarkAllAsRead` |

### Push (FCM)
- **Notification:** title and body in Arabic.
- **Android:** channel id **`rider_orders`**, sound **`rider_alert`**. The app must create this channel with
  `res/raw/rider_alert.*`.
- **iOS:** sound **`rider_alert.caf`**.
- **Data payload:** `{ type, action: "open_order_detail", orderId, orderCode }`.
- **`type` values:** `DeliveryLegAssigned`, `ReturnLegAssigned`, `LegUnassigned`.

---

## Admin

| Method | Path | Notes |
|---|---|---|
| GET | `/api/admin/Shift?cityId=` | `ShiftDto[]` |
| GET | `/api/admin/Shift/{id}` | `ShiftDto` |
| POST | `/api/admin/Shift` | `{ cityId, name, startTime: "HH:mm", endTime: "HH:mm", daysOfWeekMask, isActive, deliveryIds: [] }` → id |
| PUT | `/api/admin/Shift/{id}` | same body; **replaces** the rider list |
| DELETE | `/api/admin/Shift/{id}` | soft delete |
| GET | `/api/admin/AdminDelivery/ForAssign?orderId=` (or `cityId=`) | candidates sorted best-first (see below) |
| POST | `/api/admin/AdminOrder/{orderId}/AssignDelivery` | `{ orderId, assignments: [ { vehicleId, deliveryId, leg } ] }` |
| GET | `/api/admin/AdminOrder/{id}` | the existing detail, extended (see below) |
| POST / PUT | `/api/admin/City` | add / update accept `deliveryLegCommissionPercent`, `returnLegCommissionPercent` |

**ShiftDto:** `{ shiftId, cityId, cityName, name, startTime, endTime, daysOfWeekMask, isActive, isRunningNow,
riders: [{ deliveryId, fullName, mobileNumber, isOnline }] }`.

**ForAssign item:** `{ deliveryId, fullName, mobileNumber, zoneId, zoneName, status, isOnline, isInShift,
currentShiftName, currentShiftEndsAt, activeLegsCount }`.
- `status`: `1` Available (in shift and online), `2` in shift but offline, `3` off shift.
- `activeLegsCount`: unfinished trips the rider holds.

**AssignDelivery** (reassign is the same call):
- `leg` is optional. **Omitted means both legs to the same rider**, which is how the old admin works.
- A leg can be changed until it starts:
  - the delivery leg until `ReceivedFromOwner`;
  - the return leg until `ReceivedFromCustomer`.
- The return leg can be assigned in states Confirmed, DeliveryAssigned, OnWay and CustomerReceived.
- The new rider is notified, and so is a rider who lost a leg.

**Admin order detail** (`GET /api/admin/AdminOrder/{id}`), extended:
- `deliveryMenOrders[].leg`
- `deliveryOrderPaymentDetails[].leg` and `.commissionPercent`
- `handoverImages: [{ vehicleId, step, position, imageUrl, deliveryId, createdDate }]`

**City** (`CityDto`): now includes `deliveryLegCommissionPercent` and `returnLegCommissionPercent`, default 50 / 50.
In add / update they are optional; `null` keeps the current value, and the two together are ≤ 100.

## Migration `20261003120000_DeliveryRiderLegsAndShifts`

- **New columns:** `Leg` on `VO_DeliveryMenOrder` and `VO_DeliveryOrderPaymentDetail`, the unique indexes become
  (Order, Vehicle, Leg). Also city commission percents, rider online status and FCM tokens.
- **New tables:** `VO_Shift`, `VO_DeliveryShift`, `VO_OrderVehicleHandoverImage`, `VO_DeliveryNotification`.
- **Existing assignments:** each one also gets a return leg with the same rider at **0 %**. Riders were already paid
  100 % at delivery under the old rule, so they are not paid twice.
