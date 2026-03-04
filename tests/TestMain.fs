module TestMain

open Expecto

[<EntryPoint>]
let main args =
    let all =
        testList "All" [
            UsersTests.userDomainTests
        ]
    runTestsWithCLIArgs [] args all
