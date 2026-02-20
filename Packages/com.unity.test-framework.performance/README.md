# Unity Performance Testing Stub

This embedded package intentionally overrides `com.unity.test-framework.performance`.

Reason:
- Unity Editor crashes on test startup inside `Unity.PerformanceTesting.Editor.TestRunBuilder`
  during `IPrebuildSetup` execution (stack overflow recursion).

Scope:
- The project does not use `Unity.PerformanceTesting` APIs.
- This stub disables those hooks to allow normal EditMode/PlayMode test execution.
