module OrdersTests

open Expecto

let parseStatus =
    function
    | "pending" | "Pending" -> Some "Pending"
    | "confirmed" | "Confirmed" -> Some "Confirmed"
    | "shipped" | "Shipped" -> Some "Shipped"
    | "delivered" | "Delivered" -> Some "Delivered"
    | "cancelled" | "Cancelled" -> Some "Cancelled"
    | _ -> None

let calculateTotal (items: (decimal * int) list) =
    items |> List.sumBy (fun (price, qty) -> price * decimal qty)

[<Tests>]
let ordersDomainTests =
    testList "Orders.Domain" [
        testList "parseStatus" [
            test "parses lowercase pending" {
                Expect.isSome (parseStatus "pending") "pending should parse to Some"
            }
            test "parses capitalized Confirmed" {
                Expect.isSome (parseStatus "Confirmed") "Confirmed should parse to Some"
            }
            test "parses shipped" {
                Expect.isSome (parseStatus "shipped") "shipped should parse to Some"
            }
            test "parses delivered" {
                Expect.isSome (parseStatus "delivered") "delivered should parse to Some"
            }
            test "parses cancelled" {
                Expect.isSome (parseStatus "cancelled") "cancelled should parse to Some"
            }
            test "rejects unknown status" {
                Expect.isNone (parseStatus "processing") "unknown status should return None"
            }
            test "rejects empty string" {
                Expect.isNone (parseStatus "") "empty string should return None"
            }
        ]
        testList "total calculation" [
            test "calculates single item total" {
                Expect.equal (calculateTotal [(10m, 2)]) 20m "10 * 2 = 20"
            }
            test "calculates multi-item total" {
                Expect.equal (calculateTotal [(10m, 1); (5m, 3)]) 25m "10 + 15 = 25"
            }
            test "handles zero quantity" {
                Expect.equal (calculateTotal [(99m, 0)]) 0m "zero qty = zero total"
            }
        ]
    ]
