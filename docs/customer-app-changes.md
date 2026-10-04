# Customer app — backend changes

## Nearest zone

`GET /api/customer/Customer/GetZonesByCity?cityId={id}&latitude={lat}&longitude={lng}` (anonymous)

`latitude` / `longitude` are optional. With them, the list is nearest first, so the first item is the
customer's nearest zone. Without them, the list is sorted by name and `distanceKm` is `null`.
A zone saved without coordinates (latitude and longitude both 0) always has `distanceKm: null` and is
listed after the zones that have a distance.

```json
[
  { "zoneId": 12, "name": "المعادي", "latitude": 29.96, "longitude": 31.25, "distanceKm": 1.42 },
  { "zoneId": 9,  "name": "الزمالك", "latitude": 30.06, "longitude": 31.22, "distanceKm": 9.87 }
]
```

The selected zone is sent as `DestinationZoneId` to `GET /api/customer/CustomerOrder/AvailableVehicles`,
which already returns vehicles sorted by the distance from that zone.

## Customer push notifications

Sent to the customer's saved devices (`AndriodDevice` / `IosDevice`) in the customer's app language. The app reports it in `POST /api/customer/Customer/AddDevices` as `language` ("ar" / "en"), falling back to the Accept-Language header; customers who never reported one get Arabic. The app re-sends it when the user switches language.

| When | `type` |
|---|---|
| Order created (customer or admin) | 1 OrderCreated |
| Confirmed | 2 OrderConfirmed |
| Rider assigned to the delivery trip / the return trip | 9 RiderAssigned |
| On the way | 3 OrderOnWay |
| Received | 4 OrderCustomerReceived |
| Not received | 8 OrderUpdated |
| Completed | 5 OrderCompleted |
| Cancelled (by the customer or the admin) | 6 OrderCancelled |
| Order edited by the admin | 8 OrderUpdated |

Payload: `{ "type": "<int>", "action": "open_order_detail", "orderId": "<id>", "orderCode": "<code>" }`.

The merchant steps before Confirmed are internal and send nothing (they can repeat when the admin swaps vehicles).

State pushes come from one place: every `Order` state transition raises `OrderStateChangedEvent`, and
`OrderStateChangedCustomerPushHandler` sends the push. The per-command customer pushes that used to do
this (`UpdateOrderState`, `CancelOrder`, `RejectOrder`) were removed so the customer does not get the
same push twice.

## Rider name and phone

`GET /api/customer/CustomerOrder/{orderId}` (new) and `MyOrders` items include:

```json
"riders": [
  { "vehicleId": 5, "vehicleName": "Xiaomi Pro 2", "vehicleCode": "SC-1021", "leg": 1, "riderName": "أحمد محمود", "canCall": true, "riderMobileNumber": "01012345678" },
  { "vehicleId": 5, "vehicleName": "Xiaomi Pro 2", "vehicleCode": "SC-1021", "leg": 2, "riderName": "محمد علي", "canCall": false, "riderMobileNumber": null }
]
```

The phone is only shared while that trip is under way: the delivery rider (leg 1) while the order is on the way and the vehicle is not delivered yet; the return rider (leg 2) while the customer has the vehicle and waits for the pickup. The "rider assigned" push carries the name only.

## Cost lines

`GET /api/customer/CustomerOrder/{orderId}` and `MyOrders` items include:

```json
"priceBreakdown": {
  "subTotal": 2100, "serviceFees": 100, "deliveryFees": 160, "urgentFees": 0,
  "discount": 0, "previousDebt": 0, "total": 2360
}
```

`total = subTotal + serviceFees + deliveryFees + urgentFees − discount + previousDebt`. It is null only for
old orders without a totals row.

## Notification list with read state

Every customer push is also saved in `VO_CustomerNotification` (in the language it was sent in). The list
used to show the admin's notifications for the customer's orders; it now shows what the customer was sent.

| Method | Path | Notes |
|---|---|---|
| GET | `/api/customer/Customer/Notifications?skip=&take=` | newest first; each item has `isRead`, `readAt` |
| GET | `/api/customer/Customer/Notifications/UnreadCount` | `int` |
| POST | `/api/customer/Customer/Notifications/{id}/Read` | `true` |
| POST | `/api/customer/Customer/Notifications/ReadAll` | `true` |

## Push tokens

`POST /api/customer/Customer/AddDevices` now also removes the same token from any other customer, so a
phone that two people log in on only gets the pushes of whoever is logged in now.

On logout the app should forget its token:

| Method | Path | Notes |
|---|---|---|
| DELETE | `/api/customer/Customer/Devices?token={fcmToken}` | clears the token from the signed-in customer (Android or iOS slot); `true` |

## Account deletion

`DELETE /api/customer/Customer/DeleteAccount` no longer fails for customers who have orders.

- It is refused while any order is still open (any state other than Completed, Cancelled or
  "not received"): "Cannot delete account. You have active orders. Please complete or cancel them first."
- Otherwise the account is anonymized: name, phone, email, gender, images, push tokens, language and
  saved location are removed, the passport photo is removed from every kept order, the login is
  disabled and refresh tokens are revoked. Orders stay for the
  records. The same phone number (or email) can register again as a new account.
- The app must drop its stored access/refresh tokens right after a successful call.

## Register and passwords

- `POST /api/auth/Register` no longer returns `invitationCode`; the activation code only goes out by
  SMS/email.
- Register, reset password and profile password change share one rule set: at least 8 characters, with
  an uppercase letter, a lowercase letter and a digit. Messages: "Password must be at least 8 characters
  long", "Password must contain at least one digit (0-9)", "... uppercase letter (A-Z)", "... lowercase
  letter (a-z)".
- A rejected new password (reset or profile) now leaves the old password working.

## PayPal

- If the PayPal order cannot be created, `CreateOrder` still returns the error, but the order is now
  cancelled and its vehicles released, so the customer can try again straight away. It shows in `MyOrders`
  as Cancelled and the usual "order cancelled" push is sent.
- Cancelling a PayPal order that was never paid (capture not completed) has no cancellation fee and no
  refund; it is simply cancelled. A cancelled order can no longer be paid
  (`CompletePayPalPayment` → "Order is cancelled and can no longer be paid").
