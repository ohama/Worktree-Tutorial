namespace WorktreeApi.Orders

open System
open System.Collections.Concurrent
open WorktreeApi.Core

module Domain =

    type OrderItem =
        { ProductId: ProductId
          Quantity: int
          UnitPrice: decimal }

    type Order =
        { Id: OrderId
          UserId: UserId
          Items: OrderItem list
          Status: OrderStatus
          TotalAmount: decimal
          CreatedAt: DateTime }

    type CreateOrderItemRequest =
        { ProductId: string
          Quantity: int
          UnitPrice: decimal }

    type CreateOrderRequest =
        { UserId: string
          Items: CreateOrderItemRequest list }

    type UpdateOrderStatusRequest = { Status: string }

    // === In-Memory Store ===
    let private store = ConcurrentDictionary<Guid, Order>()

    let parseStatus =
        function
        | "pending" | "Pending" -> Some Pending
        | "confirmed" | "Confirmed" -> Some Confirmed
        | "shipped" | "Shipped" -> Some Shipped
        | "delivered" | "Delivered" -> Some Delivered
        | "cancelled" | "Cancelled" -> Some Cancelled
        | _ -> None

    let create (req: CreateOrderRequest) =
        match Guid.TryParse(req.UserId) with
        | false, _ -> Error "Invalid user ID"
        | true, userGuid ->
            let items: OrderItem list =
                req.Items
                |> List.choose (fun item ->
                    match Guid.TryParse(item.ProductId) with
                    | true, prodGuid ->
                        Some
                            { ProductId = ProductId prodGuid
                              Quantity = item.Quantity
                              UnitPrice = item.UnitPrice }
                    | false, _ -> None)

            if items.IsEmpty then
                Error "No valid items"
            else
                let id = Guid.NewGuid()
                let total = items |> List.sumBy (fun i -> i.UnitPrice * decimal i.Quantity)

                let order =
                    { Id = OrderId id
                      UserId = UserId userGuid
                      Items = items
                      Status = Pending
                      TotalAmount = total
                      CreatedAt = DateTime.UtcNow }

                store.[id] <- order
                Ok order

    let getAll () = store.Values |> Seq.toList

    let getById (id: Guid) =
        match store.TryGetValue(id) with
        | true, order -> Some order
        | false, _ -> None

    let updateStatus (id: Guid) (req: UpdateOrderStatusRequest) =
        match store.TryGetValue(id) with
        | false, _ -> Error "Order not found"
        | true, order ->
            match parseStatus req.Status with
            | None -> Error "Invalid status. Use: pending, confirmed, shipped, delivered, cancelled"
            | Some status ->
                let updated = { order with Status = status }
                store.[id] <- updated
                Ok updated

    let delete (id: Guid) = store.TryRemove(id) |> fst
