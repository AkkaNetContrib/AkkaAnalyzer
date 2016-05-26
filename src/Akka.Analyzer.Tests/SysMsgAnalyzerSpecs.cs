using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using TestHelper;
using Xunit;

namespace Akka.Analyzer.Tests
{
    public class SysMsgAnalyzerSpecs : CodeFixVerifier
    {

        //No diagnostics expected to show up
        [Fact]
        public void SysMsgAnaylzer_should_not_be_triggered_by_irrelevant_code()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [Fact(Skip = "Loading up a big enough source sample is a pain in the ass")]
        public void SysMsgAnaylzer_should_find_instances_where_Tell_is_used_instead_of_SendSystemMessage()
        {

        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new SysMsgAnalyzer();
        }
    }
}