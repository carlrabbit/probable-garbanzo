using Microsoft.CodeAnalysis.CodeRefactorings;
using Roslynator.Testing.CSharp.Xunit;

namespace Roslynator.Testing.CSharp.TUnit;

/// <summary>
/// Represents a verifier for a C# code refactoring.
/// </summary>
public abstract class TUnitRefactoringVerifier<TRefactoringProvider> : XunitRefactoringVerifier<TRefactoringProvider>
    where TRefactoringProvider : CodeRefactoringProvider, new()
{
    /// <summary>
    /// Initializes a new instance of <see cref="TUnitRefactoringVerifier{TRefactoringProvider}"/>.
    /// </summary>
    protected TUnitRefactoringVerifier()
    {
    }
}
