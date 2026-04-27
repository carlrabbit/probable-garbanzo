using Analyzers;
using Microsoft.CodeAnalysis;

namespace TUnit.Tests;

/// <summary>
/// TUnit tests for the <see cref="WholesomeCommentAnalyzer"/> XML010 rule (missing or empty summary tag).
/// </summary>
public class WholesomeCommentAnalyzerSummaryTests
    : TUnitDiagnosticVerifier<WholesomeCommentAnalyzer, NoOpCodeFixProvider>
{
    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => WholesomeCommentAnalyzer.MissingSummaryRule;

    [Test]
    public async Task Diagnostic_WhenPublicClassHasNoDocComment()
    {
        await VerifyDiagnosticAsync("""
            public class [|MyClass|] { }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenPublicClassHasEmptySummary()
    {
        await VerifyDiagnosticAsync("""
            /// <summary></summary>
            public class [|MyClass|] { }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenPublicClassHasWhitespaceSummary()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>   </summary>
            public class [|MyClass|] { }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenPublicClassHasNonEmptySummary()
    {
        await VerifyNoDiagnosticAsync("""
            /// <summary>A useful class.</summary>
            public class MyClass { }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenPrivateClassHasNoDocComment()
    {
        await VerifyNoDiagnosticAsync("""
            /// <summary>The outer class.</summary>
            public class Outer
            {
                private class Inner { }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenMemberHasInheritdoc()
    {
        await VerifyNoDiagnosticAsync("""
            /// <summary>Base class.</summary>
            public class Base { }

            /// <inheritdoc/>
            public class Derived : Base { }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenPublicMethodHasNoDocComment()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>A class.</summary>
            public class MyClass
            {
                public void [|MyMethod|]() { }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenPublicMethodHasNonEmptySummary()
    {
        await VerifyNoDiagnosticAsync("""
            /// <summary>A class.</summary>
            public class MyClass
            {
                /// <summary>Does something.</summary>
                public void MyMethod() { }
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenPublicPropertyHasNoDocComment()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>A class.</summary>
            public class MyClass
            {
                public int [|Value|] { get; set; }
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenPublicFieldHasNoDocComment()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>A class.</summary>
            public class MyClass
            {
                public int [|value|];
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenPrivateFieldHasNoDocComment()
    {
        await VerifyNoDiagnosticAsync("""
            /// <summary>A class.</summary>
            public class MyClass
            {
                private int value;
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenPublicEnumHasNoDocComment()
    {
        await VerifyDiagnosticAsync("""
            public enum [|MyEnum|]
            {
                /// <summary>First value.</summary>
                A,
                /// <summary>Second value.</summary>
                B
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenPublicEnumMemberHasNoDocComment()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>An enum.</summary>
            public enum MyEnum
            {
                [|A|]
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenPublicInterfaceHasNoDocComment()
    {
        await VerifyDiagnosticAsync("""
            public interface [|IMyInterface|] { }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenPublicStructHasNoDocComment()
    {
        await VerifyDiagnosticAsync("""
            public struct [|MyStruct|] { }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenPublicDelegateHasNoDocComment()
    {
        await VerifyDiagnosticAsync("""
            public delegate void [|MyDelegate|]();
            """);
    }

    [Test]
    public async Task Diagnostic_WhenPublicRecordHasNoDocComment()
    {
        await VerifyDiagnosticAsync("""
            public record [|MyRecord|](int Value);
            """);
    }

    [Test]
    public async Task Diagnostic_WhenPublicConstructorHasNoDocComment()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>A class.</summary>
            public class MyClass
            {
                public [|MyClass|]() { }
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenPublicEventHasNoDocComment()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>A class.</summary>
            public class MyClass
            {
                public event System.EventHandler [|MyEvent|];
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenInternalClassHasNonEmptySummary()
    {
        await VerifyNoDiagnosticAsync("""
            /// <summary>An internal class.</summary>
            internal class MyClass { }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenInternalClassHasNoDocComment()
    {
        await VerifyDiagnosticAsync("""
            internal class [|MyClass|] { }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenDefaultPrivateClassMemberHasNoDocComment()
    {
        // Class members without an access modifier are private by default
        await VerifyNoDiagnosticAsync("""
            /// <summary>A class.</summary>
            public class MyClass
            {
                void ImplicitlyPrivateMethod() { }
            }
            """);
    }
}

/// <summary>
/// TUnit tests for the <see cref="WholesomeCommentAnalyzer"/> XML011 rule (missing or empty returns tag).
/// </summary>
public class WholesomeCommentAnalyzerReturnsTests
    : TUnitDiagnosticVerifier<WholesomeCommentAnalyzer, NoOpCodeFixProvider>
{
    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => WholesomeCommentAnalyzer.MissingReturnsRule;

    [Test]
    public async Task Diagnostic_WhenNonVoidMethodHasNoReturnsTag()
    {
        await VerifyDiagnosticAsync("""
            public class MyClass
            {
                /// <summary>Gets a value.</summary>
                public int [|GetValue|]() => 0;
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenNonVoidMethodHasEmptyReturnsTag()
    {
        await VerifyDiagnosticAsync("""
            public class MyClass
            {
                /// <summary>Gets a value.</summary>
                /// <returns></returns>
                public int [|GetValue|]() => 0;
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenNonVoidMethodHasNonEmptyReturnsTag()
    {
        await VerifyNoDiagnosticAsync("""
            public class MyClass
            {
                /// <summary>Gets a value.</summary>
                /// <returns>The value.</returns>
                public int GetValue() => 0;
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenVoidMethodHasNoReturnsTag()
    {
        await VerifyNoDiagnosticAsync("""
            public class MyClass
            {
                /// <summary>Does something.</summary>
                public void DoSomething() { }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenTaskMethodHasNoReturnsTag()
    {
        await VerifyNoDiagnosticAsync("""
            using System.Threading.Tasks;
            public class MyClass
            {
                /// <summary>Does something async.</summary>
                public Task DoSomethingAsync() => Task.CompletedTask;
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenValueTaskMethodHasNoReturnsTag()
    {
        await VerifyNoDiagnosticAsync("""
            using System.Threading.Tasks;
            public class MyClass
            {
                /// <summary>Does something async.</summary>
                public ValueTask DoSomethingAsync() => ValueTask.CompletedTask;
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenTaskGenericMethodHasNoReturnsTag()
    {
        await VerifyDiagnosticAsync("""
            using System.Threading.Tasks;
            public class MyClass
            {
                /// <summary>Gets a value asynchronously.</summary>
                public Task<int> [|GetValueAsync|]() => Task.FromResult(0);
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenPropertyHasNoReturnsTag()
    {
        await VerifyDiagnosticAsync("""
            public class MyClass
            {
                /// <summary>Gets the value.</summary>
                public int [|Value|] { get; }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenPropertyHasNonEmptyReturnsTag()
    {
        await VerifyNoDiagnosticAsync("""
            public class MyClass
            {
                /// <summary>Gets the value.</summary>
                /// <returns>The value.</returns>
                public int Value { get; }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenNoDocComment()
    {
        // Returns rule only fires when a doc comment is present but missing <returns>
        await VerifyNoDiagnosticAsync("""
            public class MyClass
            {
                public int GetValue() => 0;
            }
            """);
    }
}

/// <summary>
/// TUnit tests for the <see cref="WholesomeCommentAnalyzer"/> XML012 rule (missing or empty param tag).
/// </summary>
public class WholesomeCommentAnalyzerParamTests
    : TUnitDiagnosticVerifier<WholesomeCommentAnalyzer, NoOpCodeFixProvider>
{
    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => WholesomeCommentAnalyzer.MissingParamRule;

    [Test]
    public async Task Diagnostic_WhenMethodHasUndocumentedParameter()
    {
        await VerifyDiagnosticAsync("""
            public class MyClass
            {
                /// <summary>Does something.</summary>
                public void MyMethod(string [|value|]) { }
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenMethodHasEmptyParamTag()
    {
        await VerifyDiagnosticAsync("""
            public class MyClass
            {
                /// <summary>Does something.</summary>
                /// <param name="value"></param>
                public void MyMethod(string [|value|]) { }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenMethodHasNonEmptyParamTag()
    {
        await VerifyNoDiagnosticAsync("""
            public class MyClass
            {
                /// <summary>Does something.</summary>
                /// <param name="value">The value.</param>
                public void MyMethod(string value) { }
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenOneOfMultipleParamsIsUndocumented()
    {
        await VerifyDiagnosticAsync("""
            public class MyClass
            {
                /// <summary>Does something.</summary>
                /// <param name="a">First value.</param>
                public void MyMethod(string a, int [|b|]) { }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenMethodHasNoParameters()
    {
        await VerifyNoDiagnosticAsync("""
            public class MyClass
            {
                /// <summary>Does something.</summary>
                public void MyMethod() { }
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenConstructorHasUndocumentedParameter()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>A class.</summary>
            public class MyClass
            {
                /// <summary>Creates an instance.</summary>
                public MyClass(int [|value|]) { }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenNoDocComment()
    {
        // Param rule only fires when a doc comment is present
        await VerifyNoDiagnosticAsync("""
            public class MyClass
            {
                public void MyMethod(string value) { }
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenDelegateHasUndocumentedParameter()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>A delegate.</summary>
            public delegate void MyDelegate(int [|value|]);
            """);
    }
}

/// <summary>
/// TUnit tests for the <see cref="WholesomeCommentAnalyzer"/> XML013 rule (missing or empty typeparam tag).
/// </summary>
public class WholesomeCommentAnalyzerTypeParamTests
    : TUnitDiagnosticVerifier<WholesomeCommentAnalyzer, NoOpCodeFixProvider>
{
    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => WholesomeCommentAnalyzer.MissingTypeParamRule;

    [Test]
    public async Task Diagnostic_WhenGenericClassHasUndocumentedTypeParameter()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>A generic class.</summary>
            public class MyClass<[|T|]> { }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenGenericClassHasEmptyTypeparamTag()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>A generic class.</summary>
            /// <typeparam name="T"></typeparam>
            public class MyClass<[|T|]> { }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenGenericClassHasNonEmptyTypeparamTag()
    {
        await VerifyNoDiagnosticAsync("""
            /// <summary>A generic class.</summary>
            /// <typeparam name="T">The element type.</typeparam>
            public class MyClass<T> { }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenGenericMethodHasUndocumentedTypeParameter()
    {
        await VerifyDiagnosticAsync("""
            public class MyClass
            {
                /// <summary>Does something generic.</summary>
                public void MyMethod<[|T|]>(T value) { }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenGenericMethodHasNonEmptyTypeparamTag()
    {
        await VerifyNoDiagnosticAsync("""
            public class MyClass
            {
                /// <summary>Does something generic.</summary>
                /// <typeparam name="T">The element type.</typeparam>
                /// <param name="value">The value.</param>
                public void MyMethod<T>(T value) { }
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenOneOfMultipleTypeParamsIsUndocumented()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>A generic class.</summary>
            /// <typeparam name="TKey">The key type.</typeparam>
            public class MyClass<TKey, [|TValue|]> { }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenNonGenericMember()
    {
        await VerifyNoDiagnosticAsync("""
            /// <summary>A class.</summary>
            public class MyClass
            {
                /// <summary>Does something.</summary>
                public void MyMethod() { }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenNoDocComment()
    {
        // TypeParam rule only fires when a doc comment is present
        await VerifyNoDiagnosticAsync("""
            public class MyClass<T> { }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenGenericDelegateHasUndocumentedTypeParameter()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>A generic delegate.</summary>
            public delegate void MyDelegate<[|T|]>(T value);
            """);
    }
}
