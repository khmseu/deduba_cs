# more plans (merged into docs/FUTURE_PLANS.md)

This file is kept for reference and history only; the contents were consolidated into `docs/FUTURE_PLANS.md` (see Appendix: More Plans).

- Common Shim Names - this will require refactoring the existing code
  - For an OS function named `SomeFunction`:
    - Encapsulate it with a C++ method `OSNAME_SomeFunction`
    - Have a result translator private method `handle_SomeFunction` on the native side
    - Encapsulate that with a C# function `OsNameSomeFunction`
- The high-level OS API then uses those C# functions
