module ProductsTests

open Expecto

let validateProduct (price: decimal) (stock: int) =
    if price < 0m then Error "Price must be non-negative"
    elif stock < 0 then Error "Stock must be non-negative"
    else Ok ()

[<Tests>]
let productDomainTests =
    testList "Products.Domain" [
        testList "create validation" [
            test "accepts valid price and stock" {
                Expect.isOk (validateProduct 89.99m 50) "valid product should be Ok"
            }
            test "accepts zero price" {
                Expect.isOk (validateProduct 0m 0) "zero price and stock should be Ok"
            }
            test "rejects negative price" {
                Expect.isError (validateProduct -1m 10) "negative price should be Error"
            }
            test "rejects negative stock" {
                Expect.isError (validateProduct 10m -1) "negative stock should be Error"
            }
            test "rejects both negative" {
                Expect.isError (validateProduct -1m -1) "both negative: price checked first"
            }
        ]
    ]
