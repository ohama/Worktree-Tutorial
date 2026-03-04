module UsersTests

open Expecto

let parseRole =
    function
    | "admin" | "Admin" -> Some "Admin"
    | "member" | "Member" -> Some "Member"
    | "guest" | "Guest" -> Some "Guest"
    | _ -> None

[<Tests>]
let userDomainTests =
    testList "Users.Domain" [
        testList "parseRole" [
            test "parses lowercase admin" {
                Expect.isSome (parseRole "admin") "admin should parse to Some"
            }
            test "parses capitalized Admin" {
                Expect.isSome (parseRole "Admin") "Admin should parse to Some"
            }
            test "parses member" {
                Expect.isSome (parseRole "member") "member should parse to Some"
            }
            test "parses guest" {
                Expect.isSome (parseRole "guest") "guest should parse to Some"
            }
            test "rejects unknown role" {
                Expect.isNone (parseRole "superuser") "unknown role should return None"
            }
            test "rejects empty string" {
                Expect.isNone (parseRole "") "empty string should return None"
            }
        ]
    ]
