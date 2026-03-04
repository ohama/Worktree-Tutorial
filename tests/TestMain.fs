module TestMain

open Expecto

[<EntryPoint>]
let main args =
    let all =
        testList "All" [
            UsersTests.userDomainTests
            ProductsTests.productDomainTests
        ]
    runTestsWithCLIArgs [] args all
