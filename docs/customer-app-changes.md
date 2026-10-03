# Customer app — backend changes

## Nearest zone

`GET /api/customer/Customer/GetZonesByCity?cityId={id}&latitude={lat}&longitude={lng}` (anonymous)

`latitude` / `longitude` are optional. With them, the list is nearest first, so the first item is the
customer's nearest zone. Without them, the list is sorted by name and `distanceKm` is `null`.

```json
[
  { "zoneId": 12, "name": "المعادي", "latitude": 29.96, "longitude": 31.25, "distanceKm": 1.42 },
  { "zoneId": 9,  "name": "الزمالك", "latitude": 30.06, "longitude": 31.22, "distanceKm": 9.87 }
]
```

The selected zone is sent as `DestinationZoneId` to `GET /api/customer/CustomerOrder/AvailableVehicles`,
which already returns vehicles sorted by the distance from that zone.

## Customer push notifications

Sent to the customer's saved devices (`AndriodDevice` / `IosDevice`). Text is Arabic.

| When | `type` |
|---|---|
| Order created (customer or admin) | 1 OrderCreated |
| Sent to merchants | 7 OrderMerchantPending |
| Merchant confirmed the vehicles | 8 OrderUpdated |
| Confirmed | 2 OrderConfirmed |
| Rider assigned to the delivery trip / the return trip | 9 RiderAssigned |
| On the way | 3 OrderOnWay |
| Received | 4 OrderCustomerReceived |
| Not received | 8 OrderUpdated |
| Completed | 5 OrderCompleted |
| Cancelled (by the customer or the admin) | 6 OrderCancelled |
| Order edited by the admin | 8 OrderUpdated |

Payload: `{ "type": "<int>", "action": "open_order_detail", "orderId": "<id>", "orderCode": "<code>" }`.

State pushes come from one place: every `Order` state transition raises `OrderStateChangedEvent`, and
`OrderStateChangedCustomerPushHandler` sends the push. The per-command customer pushes that used to do
this (`UpdateOrderState`, `CancelOrder`, `RejectOrder`) were removed so the customer does not get the
same push twice.
