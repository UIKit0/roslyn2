// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;
using Roslyn.Test.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public partial class SyntaxBinderTests : CompilingTestBase
    {
        [Fact, WorkItem(543895, "DevDiv")]
        public void TestBug11947()
        {
            // Due to a long-standing bug, the native compiler allows underlying-enum with the same
            // semantics as enum-underlying. (That is, the math is done in the underlying type and
            // then cast back to the enum type.) 

            string source = @"
using System;
public enum E { Zero, One, Two };
class Test
{
    static void Main()
    {
        E e = E.One;
        int x = 3;
        E r = x - e;
        Console.Write(r);

        E? en = E.Two;
        int? xn = 2;
        E? rn = xn - en;
        Console.Write(rn);
    }
}";
            CompileAndVerify(source: source, expectedOutput: "TwoZero");
        }

        private const string StructWithUserDefinedBooleanOperators = @"
struct S
{
    private int num;
    private string str;

    public S(int num, char chr) 
    { 
        this.num = num;
        this.str = chr.ToString();
    }

    public S(int num, string str) 
    { 
        this.num = num;
        this.str = str;
    }

    public static S operator & (S x, S y) 
    { 
        return new S(x.num & y.num, '(' + x.str + '&' + y.str + ')'); 
    }

    public static S operator | (S x, S y) 
    { 
        return new S(x.num | y.num, '(' + x.str + '|' + y.str + ')'); 
    }

    public static bool operator true(S s) 
    { 
        return s.num != 0; 
    }

    public static bool operator false(S s) 
    { 
        return s.num == 0; 
    }

    public override string ToString() 
    { 
        return this.num.ToString() + ':' + this.str; 
    }
}
";

        [Fact]
        public void TestUserDefinedLogicalOperators()
        {
            string source = @"
using System;

class C
{
    static void Main()
    {
        S f = new S(0, 'f');
        S t = new S(1, 't');
        Console.WriteLine((f && f) && f);
        Console.WriteLine((f && f) && t);
        Console.WriteLine((f && t) && f);
        Console.WriteLine((f && t) && t);
        Console.WriteLine((t && f) && f);
        Console.WriteLine((t && f) && t);
        Console.WriteLine((t && t) && f);
        Console.WriteLine((t && t) && t);
        Console.WriteLine('-');
        Console.WriteLine((f && f) || f);
        Console.WriteLine((f && f) || t);
        Console.WriteLine((f && t) || f);
        Console.WriteLine((f && t) || t);
        Console.WriteLine((t && f) || f);
        Console.WriteLine((t && f) || t);
        Console.WriteLine((t && t) || f);
        Console.WriteLine((t && t) || t);
        Console.WriteLine('-');
        Console.WriteLine((f || f) && f);
        Console.WriteLine((f || f) && t);
        Console.WriteLine((f || t) && f);
        Console.WriteLine((f || t) && t);
        Console.WriteLine((t || f) && f);
        Console.WriteLine((t || f) && t);
        Console.WriteLine((t || t) && f);
        Console.WriteLine((t || t) && t);
        Console.WriteLine('-');
        Console.WriteLine((f || f) || f);
        Console.WriteLine((f || f) || t);
        Console.WriteLine((f || t) || f);
        Console.WriteLine((f || t) || t);
        Console.WriteLine((t || f) || f);
        Console.WriteLine((t || f) || t);
        Console.WriteLine((t || t) || f);
        Console.WriteLine((t || t) || t);
        Console.WriteLine('-');
        Console.WriteLine(f && (f && f));
        Console.WriteLine(f && (f && t));
        Console.WriteLine(f && (t && f));
        Console.WriteLine(f && (t && t));
        Console.WriteLine(t && (f && f));
        Console.WriteLine(t && (f && t));
        Console.WriteLine(t && (t && f));
        Console.WriteLine(t && (t && t));
        Console.WriteLine('-');
        Console.WriteLine(f && (f || f));
        Console.WriteLine(f && (f || t));
        Console.WriteLine(f && (t || f));
        Console.WriteLine(f && (t || t));
        Console.WriteLine(t && (f || f));
        Console.WriteLine(t && (f || t));
        Console.WriteLine(t && (t || f));
        Console.WriteLine(t && (t || t));
        Console.WriteLine('-');
        Console.WriteLine(f || (f && f));
        Console.WriteLine(f || (f && t));
        Console.WriteLine(f || (t && f));
        Console.WriteLine(f || (t && t));
        Console.WriteLine(t || (f && f));
        Console.WriteLine(t || (f && t));
        Console.WriteLine(t || (t && f));
        Console.WriteLine(t || (t && t));
        Console.WriteLine('-');
        Console.WriteLine(f || (f || f));
        Console.WriteLine(f || (f || t));
        Console.WriteLine(f || (t || f));
        Console.WriteLine(f || (t || t));
        Console.WriteLine(t || (f || f));
        Console.WriteLine(t || (f || t));
        Console.WriteLine(t || (t || f));
        Console.WriteLine(t || (t || t));
    }
}

" + StructWithUserDefinedBooleanOperators;

            string output = @"0:f
0:f
0:f
0:f
0:(t&f)
0:(t&f)
0:((t&t)&f)
1:((t&t)&t)
-
0:(f|f)
1:(f|t)
0:(f|f)
1:(f|t)
0:((t&f)|f)
1:((t&f)|t)
1:(t&t)
1:(t&t)
-
0:(f|f)
0:(f|f)
0:((f|t)&f)
1:((f|t)&t)
0:(t&f)
1:(t&t)
0:(t&f)
1:(t&t)
-
0:((f|f)|f)
1:((f|f)|t)
1:(f|t)
1:(f|t)
1:t
1:t
1:t
1:t
-
0:f
0:f
0:f
0:f
0:(t&f)
0:(t&f)
0:(t&(t&f))
1:(t&(t&t))
-
0:f
0:f
0:f
0:f
0:(t&(f|f))
1:(t&(f|t))
1:(t&t)
1:(t&t)
-
0:(f|f)
0:(f|f)
0:(f|(t&f))
1:(f|(t&t))
1:t
1:t
1:t
1:t
-
0:(f|(f|f))
1:(f|(f|t))
1:(f|t)
1:(f|t)
1:t
1:t
1:t
1:t";

            CompileAndVerify(source: source, expectedOutput: output);
        }

        [Fact]
        public void TestUserDefinedLogicalOperators2()
        {
            string source = @"
using System;

class C
{
    static void Main()
    {
        S f = new S(0, 'f');
        S t = new S(1, 't');
        Console.Write((f && f) && f ? 1 : 0);
        Console.Write((f && f) && t ? 1 : 0);
        Console.Write((f && t) && f ? 1 : 0);
        Console.Write((f && t) && t ? 1 : 0);
        Console.Write((t && f) && f ? 1 : 0);
        Console.Write((t && f) && t ? 1 : 0);
        Console.Write((t && t) && f ? 1 : 0);
        Console.Write((t && t) && t ? 1 : 0);
        Console.WriteLine('-');
        Console.Write((f && f) || f ? 1 : 0);
        Console.Write((f && f) || t ? 1 : 0);
        Console.Write((f && t) || f ? 1 : 0);
        Console.Write((f && t) || t ? 1 : 0);
        Console.Write((t && f) || f ? 1 : 0);
        Console.Write((t && f) || t ? 1 : 0);
        Console.Write((t && t) || f ? 1 : 0);
        Console.Write((t && t) || t ? 1 : 0);
        Console.WriteLine('-');
        Console.Write((f || f) && f ? 1 : 0);
        Console.Write((f || f) && t ? 1 : 0);
        Console.Write((f || t) && f ? 1 : 0);
        Console.Write((f || t) && t ? 1 : 0);
        Console.Write((t || f) && f ? 1 : 0);
        Console.Write((t || f) && t ? 1 : 0);
        Console.Write((t || t) && f ? 1 : 0);
        Console.Write((t || t) && t ? 1 : 0);
        Console.WriteLine('-');
        Console.Write((f || f) || f ? 1 : 0);
        Console.Write((f || f) || t ? 1 : 0);
        Console.Write((f || t) || f ? 1 : 0);
        Console.Write((f || t) || t ? 1 : 0);
        Console.Write((t || f) || f ? 1 : 0);
        Console.Write((t || f) || t ? 1 : 0);
        Console.Write((t || t) || f ? 1 : 0);
        Console.Write((t || t) || t ? 1 : 0);
        Console.WriteLine('-');       
        Console.Write(f && (f && f) ? 1 : 0);
        Console.Write(f && (f && t) ? 1 : 0);
        Console.Write(f && (t && f) ? 1 : 0);
        Console.Write(f && (t && t) ? 1 : 0);
        Console.Write(t && (f && f) ? 1 : 0);
        Console.Write(t && (f && t) ? 1 : 0);
        Console.Write(t && (t && f) ? 1 : 0);
        Console.Write(t && (t && t) ? 1 : 0);
        Console.WriteLine('-');      
        Console.Write(f && (f || f) ? 1 : 0);
        Console.Write(f && (f || t) ? 1 : 0);
        Console.Write(f && (t || f) ? 1 : 0);
        Console.Write(f && (t || t) ? 1 : 0);
        Console.Write(t && (f || f) ? 1 : 0);
        Console.Write(t && (f || t) ? 1 : 0);
        Console.Write(t && (t || f) ? 1 : 0);
        Console.Write(t && (t || t) ? 1 : 0);
        Console.WriteLine('-');        
        Console.Write(f || (f && f) ? 1 : 0);
        Console.Write(f || (f && t) ? 1 : 0);
        Console.Write(f || (t && f) ? 1 : 0);
        Console.Write(f || (t && t) ? 1 : 0);
        Console.Write(t || (f && f) ? 1 : 0);
        Console.Write(t || (f && t) ? 1 : 0);
        Console.Write(t || (t && f) ? 1 : 0);
        Console.Write(t || (t && t) ? 1 : 0);
        Console.WriteLine('-');        
        Console.Write(f || (f || f) ? 1 : 0);
        Console.Write(f || (f || t) ? 1 : 0);
        Console.Write(f || (t || f) ? 1 : 0);
        Console.Write(f || (t || t) ? 1 : 0);
        Console.Write(t || (f || f) ? 1 : 0);
        Console.Write(t || (f || t) ? 1 : 0);
        Console.Write(t || (t || f) ? 1 : 0);
        Console.Write(t || (t || t) ? 1 : 0);
    }
}
" + StructWithUserDefinedBooleanOperators;

            string output = @"
00000001-
01010111-
00010101-
01111111-
00000001-
00000111-
00011111-
01111111";

            CompileAndVerify(source: source, expectedOutput: output);
        }

        [Fact]
        public void TestOperatorTrue()
        {
            string source = @"
using System;
struct S
{
    private int x;
    public S(int x) { this.x = x; }
    public static bool operator true(S s) { return s.x != 0; }
    public static bool operator false(S s) { return s.x == 0; }
}

class C
{
    static void Main()
    {
        S zero = new S(0);
        S one = new S(1);

        if (zero)
            Console.Write('a');
        else
            Console.Write('b');

        if (one)
            Console.Write('c');
        else
            Console.Write('d');

        Console.Write( zero ? 'e' : 'f' );
        Console.Write( one ? 'g' : 'h' );

        while(zero)
        {
            Console.Write('i');
        }

        while(one)
        {
            Console.Write('j');
            break;
        }

        do
        {
            Console.Write('k');
        }
        while(zero);

        bool first = true;
        do
        {
            Console.Write('l');
            if (!first) break;
            first = false;
        }
        while(one);

        for( ; zero ; )
        {
            Console.Write('m');
        }

        for( ; one ; )
        {
            Console.Write('n');
            break;
        }
    }
}";
            string output = @"bcfgjklln";

            CompileAndVerify(source: source, expectedOutput: output);
        }

        [Fact]
        public void TestUnaryOperatorOverloading()
        {
            string source = @"
using System;
struct S
{
    private string str;
    public S(char chr) { this.str = chr.ToString(); }
    public S(string str) { this.str = str; }
    public static S operator + (S x) { return new S('(' + ('+' + x.str) + ')'); }
    public static S operator - (S x) { return new S('(' + ('-' + x.str) + ')'); }
    public static S operator ~ (S x) { return new S('(' + ('~' + x.str) + ')'); }
    public static S operator ! (S x) { return new S('(' + ('!' + x.str) + ')'); }
    public static S operator ++(S x) { return new S('(' + x.str + '+' + '1' + ')'); }
    public static S operator --(S x) { return new S('(' + x.str + '-' + '1' + ')'); }
    public override string ToString() { return this.str; }
}

class C
{
    static void Main()
    {
        S a = new S('a');
        S b = new S('b');
        S c = new S('c');
        S d = new S('d');

        Console.Write( + ~ ! - a );
        Console.Write( a );
        Console.Write( a++ );
        Console.Write( a );
        Console.Write( b );
        Console.Write( ++b );
        Console.Write( b );
        Console.Write( c );
        Console.Write( c-- );
        Console.Write( c );
        Console.Write( d );
        Console.Write( --d );
        Console.Write( d );
    }
}";
            string output = "(+(~(!(-a))))aa(a+1)b(b+1)(b+1)cc(c-1)d(d-1)(d-1)";

            CompileAndVerify(source: source, expectedOutput: output);
        }

        [Fact]
        public void TestBinaryOperatorOverloading()
        {
            string source = @"
using System;
struct S
{
    private string str;
    public S(char chr) { this.str = chr.ToString(); }
    public S(string str) { this.str = str; }
    public static S operator + (S x, S y) { return new S('(' + x.str + '+' + y.str + ')'); }
    public static S operator - (S x, S y) { return new S('(' + x.str + '-' + y.str + ')'); }
    public static S operator % (S x, S y) { return new S('(' + x.str + '%' + y.str + ')'); }
    public static S operator / (S x, S y) { return new S('(' + x.str + '/' + y.str + ')'); }
    public static S operator * (S x, S y) { return new S('(' + x.str + '*' + y.str + ')'); }
    public static S operator & (S x, S y) { return new S('(' + x.str + '&' + y.str + ')'); }
    public static S operator | (S x, S y) { return new S('(' + x.str + '|' + y.str + ')'); }
    public static S operator ^ (S x, S y) { return new S('(' + x.str + '^' + y.str + ')'); }
    public static S operator << (S x, int y) { return new S('(' + x.str + '<' + '<' + y.ToString() + ')'); }
    public static S operator >> (S x, int y) { return new S('(' + x.str + '>' + '>' + y.ToString() + ')'); }
    public static S operator == (S x, S y) { return new S('(' + x.str + '=' + '=' + y.str + ')'); }
    public static S operator != (S x, S y) { return new S('(' + x.str + '!' + '=' + y.str + ')'); }
    public static S operator >= (S x, S y) { return new S('(' + x.str + '>' + '=' + y.str + ')'); }
    public static S operator <= (S x, S y) { return new S('(' + x.str + '<' + '=' + y.str + ')'); }
    public static S operator > (S x, S y) { return new S('(' + x.str + '>' + y.str + ')'); }
    public static S operator < (S x, S y) { return new S('(' + x.str + '<' + y.str + ')'); }
    public override string ToString() { return this.str; }
}

class C
{
    static void Main()
    {
        S a = new S('a');
        S b = new S('b');    
        S c = new S('c');    
        S d = new S('d');    
        S e = new S('e');    
        S f = new S('f');    
        S g = new S('g');    
        S h = new S('h');    
        S i = new S('i');    
        S j = new S('j');    
        S k = new S('k');    
        S l = new S('l');    
        S m = new S('m');    
        S n = new S('n');    
        S o = new S('o');    
        S p = new S('p');    

        Console.WriteLine(
            (a >> 10) + (b << 20) - c * d / e % f & g |
            h ^ i == j != k < l > m <= o >= p);
    }
}";
            string output = @"(((((a>>10)+(b<<20))-(((c*d)/e)%f))&g)|(h^((i==j)!=((((k<l)>m)<=o)>=p))))";

            CompileAndVerify(source : source, expectedOutput : output);
        }

        [Fact, WorkItem(657084, "DevDiv")]
        public void DuplicateOperatorInSubclass()
        {
            string source = @"
public class B 
{
    public static B operator +(C c, B b) { return null; }
}
 
public class C : B 
{
    public static B operator +(C c, B b) { return null; }
}
 
public class Test 
{
    public static void Main() 
    {
        B b = new C() + new B(); 
    }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (16,15): error CS0034: Operator '+' is ambiguous on operands of type 'C' and 'B'
                Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "new C() + new B()").WithArguments("+", "C", "B"));
        }

        [Fact, WorkItem(624274, "DevDiv")]
        public void TestBinaryOperatorOverloading_Enums_Dynamic_Unambiguous()
        {
            string source = @"
#pragma warning disable 219 // The variable is assigned but its value is never used

using System.Collections.Generic;

class C<T>
{    
    enum E { A }  

    public void M()
    {
        var eq1 = C<dynamic>.E.A == C<object>.E.A;
        var eq2 = C<object>.E.A == C<dynamic>.E.A;
        var eq3 = C<Dictionary<object, dynamic>>.E.A == C<Dictionary<dynamic, object>>.E.A;

        var neq1 = C<dynamic>.E.A != C<object>.E.A;
        var neq2 = C<object>.E.A != C<dynamic>.E.A;
        var neq3 = C<Dictionary<object, dynamic>>.E.A != C<Dictionary<dynamic, object>>.E.A;

        var lt1 = C<dynamic>.E.A < C<object>.E.A;
        var lt2 = C<object>.E.A < C<dynamic>.E.A;
        var lt3 = C<Dictionary<object, dynamic>>.E.A < C<Dictionary<dynamic, object>>.E.A;

        var lte1 = C<dynamic>.E.A <= C<object>.E.A;
        var lte2 = C<object>.E.A <= C<dynamic>.E.A;
        var lte3 = C<Dictionary<object, dynamic>>.E.A <= C<Dictionary<dynamic, object>>.E.A;

        var gt1 = C<dynamic>.E.A > C<object>.E.A;
        var gt2 = C<object>.E.A > C<dynamic>.E.A;
        var gt3 = C<Dictionary<object, dynamic>>.E.A > C<Dictionary<dynamic, object>>.E.A;

        var gte1 = C<dynamic>.E.A >= C<object>.E.A;
        var gte2 = C<object>.E.A >= C<dynamic>.E.A;
        var gte3 = C<Dictionary<object, dynamic>>.E.A >= C<Dictionary<dynamic, object>>.E.A;

        var sub1 = C<dynamic>.E.A - C<object>.E.A;
        var sub2 = C<object>.E.A - C<dynamic>.E.A;
        var sub3 = C<Dictionary<object, dynamic>>.E.A - C<Dictionary<dynamic, object>>.E.A;

        var subu1 = C<dynamic>.E.A - 1;
        var subu3 = C<Dictionary<object, dynamic>>.E.A - 1;

        var usub1 = 1 - C<dynamic>.E.A;
        var usub3 = 1 - C<Dictionary<object, dynamic>>.E.A;

        var addu1 = C<dynamic>.E.A + 1;
        var addu3 = C<Dictionary<object, dynamic>>.E.A + 1;

        var uadd1 = 1 + C<dynamic>.E.A;
        var uadd3 = 1 + C<Dictionary<object, dynamic>>.E.A;
    }
}
";
            CreateCompilationWithMscorlibAndSystemCore(source).VerifyDiagnostics();
        }

        [Fact, WorkItem(624274, "DevDiv")]
        public void TestBinaryOperatorOverloading_Enums_Dynamic_Ambiguous()
        {
            string source = @"
#pragma warning disable 219 // The variable is assigned but its value is never used

class C<T>
{    
    enum E { A }  

    public void M()
    {
        var and = C<dynamic>.E.A & C<object>.E.A;
        var or = C<dynamic>.E.A | C<object>.E.A;
        var xor = C<dynamic>.E.A ^ C<object>.E.A;
    }
}
";
            CreateCompilationWithMscorlibAndSystemCore(source).VerifyDiagnostics(
                // (10,19): error CS0034: Operator '&' is ambiguous on operands of type 'C<dynamic>.E' and 'C<object>.E'
                Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "C<dynamic>.E.A & C<object>.E.A").WithArguments("&", "C<dynamic>.E", "C<object>.E"),
                // (11,18): error CS0034: Operator '|' is ambiguous on operands of type 'C<dynamic>.E' and 'C<object>.E'
                Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "C<dynamic>.E.A | C<object>.E.A").WithArguments("|", "C<dynamic>.E", "C<object>.E"),
                // (12,19): error CS0034: Operator '^' is ambiguous on operands of type 'C<dynamic>.E' and 'C<object>.E'
                Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "C<dynamic>.E.A ^ C<object>.E.A").WithArguments("^", "C<dynamic>.E", "C<object>.E"));
        }

        [Fact]
        [WorkItem(624270, "DevDiv"), WorkItem(624274, "DevDiv")]
        public void TestBinaryOperatorOverloading_Delegates_Dynamic_Unambiguous()
        {
            string source = @"
#pragma warning disable 219 // The variable is assigned but its value is never used

class C<T>
{    
    delegate void A<U, V>(U u, V v);

    C<dynamic>.A<object, object> d1 = null;
    C<object>.A<object, object> d2 = null;

    C<dynamic>.A<object, dynamic> d3 = null;
    C<object>.A<dynamic, object> d4 = null;

    public void M()
    {
        var eq1 = d1 == d2;
        var eq2 = d1 == d3;
        var eq3 = d1 == d4;
        var eq4 = d2 == d3;
        
        var neq1 = d1 != d2;
        var neq2 = d1 != d3;
        var neq3 = d1 != d4;
        var neq4 = d2 != d3;
    }      
}
";
            // Dev11 reports error CS0034: Operator '...' is ambiguous on operands ... and ... for all combinations
            CreateCompilationWithMscorlibAndSystemCore(source).VerifyDiagnostics();
        }

        [Fact]
        public void TestBinaryOperatorOverloading_UserDefined_Dynamic_Unambiguous()
        {
            string source = @"
public class D<T>
{
	public class C
	{
		public static int operator +(C x, C y) { return 1; }
	}
}

class X
{
	static void Main()
	{
		var x = new D<object>.C();
		var y = new D<dynamic>.C();
		var z = x + y;
	}
}";
            // Dev11 reports error CS0121: The call is ambiguous between the following methods or properties: 
            // 'D<object>.C.operator+(D<object>.C, D<object>.C)' and 'D<dynamic>.C.operator +(D<dynamic>.C, D<dynamic>.C)'
            CreateCompilationWithMscorlibAndSystemCore(source).VerifyDiagnostics();
        }

        [Fact]
        public void TestBinaryOperatorOverloading_UserDefined_Dynamic_Ambiguous()
        {
            string source = @"
public class D<T>
{
	public class C
	{
		public static C operator +(C x, C y) { return null; }
	}
}

class X
{
	static void Main()
	{
		var x = new D<object>.C();
		var y = new D<dynamic>.C();
		var z = x + y;
	}
}";
            CreateCompilationWithMscorlibAndSystemCore(source).VerifyDiagnostics(
                // (16,11): error CS0034: Operator '+' is ambiguous on operands of type 'D<object>.C' and 'D<dynamic>.C'
                Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "x + y").WithArguments("+", "D<object>.C", "D<dynamic>.C"));
        }

        [Fact]
        [WorkItem(624270, "DevDiv"), WorkItem(624274, "DevDiv")]
        public void TestBinaryOperatorOverloading_Delegates_Dynamic_Ambiguous()
        {
            string source = @"
#pragma warning disable 219 // The variable is assigned but its value is never used

class C<T>
{    
    delegate void A<U, V>(U u, V v);

    C<dynamic>.A<object, object> d1 = null;
    C<object>.A<object, object> d2 = null;

    C<dynamic>.A<object, dynamic> d3 = null;
    C<object>.A<dynamic, object> d4 = null;

    public void M()
    {
        var add1 = d1 + d2;
        var add2 = d1 + d3;
        var add3 = d1 + d4;
        var add4 = d2 + d3;

        var sub1 = d1 - d2;
        var sub2 = d1 - d3;
        var sub3 = d1 - d4;
        var sub4 = d2 - d3;
    }      
}
";
            CreateCompilationWithMscorlibAndSystemCore(source).VerifyDiagnostics(
                // (17,20): error CS0034: Operator '+' is ambiguous on operands of type 'C<dynamic>.A<object, object>' and 'C<object>.A<object, object>'
                Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "d1 + d2").WithArguments("+", "C<dynamic>.A<object, object>", "C<object>.A<object, object>"),
                // (18,20): error CS0034: Operator '+' is ambiguous on operands of type 'C<dynamic>.A<object, object>' and 'C<dynamic>.A<object, dynamic>'
                Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "d1 + d3").WithArguments("+", "C<dynamic>.A<object, object>", "C<dynamic>.A<object, dynamic>"),
                // (19,20): error CS0034: Operator '+' is ambiguous on operands of type 'C<dynamic>.A<object, object>' and 'C<object>.A<dynamic, object>'
                Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "d1 + d4").WithArguments("+", "C<dynamic>.A<object, object>", "C<object>.A<dynamic, object>"),
                // (20,20): error CS0034: Operator '+' is ambiguous on operands of type 'C<object>.A<object, object>' and 'C<dynamic>.A<object, dynamic>'
                Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "d2 + d3").WithArguments("+", "C<object>.A<object, object>", "C<dynamic>.A<object, dynamic>"),
                // (22,20): error CS0034: Operator '-' is ambiguous on operands of type 'C<dynamic>.A<object, object>' and 'C<object>.A<object, object>'
                Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "d1 - d2").WithArguments("-", "C<dynamic>.A<object, object>", "C<object>.A<object, object>"),
                // (23,20): error CS0034: Operator '-' is ambiguous on operands of type 'C<dynamic>.A<object, object>' and 'C<dynamic>.A<object, dynamic>'
                Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "d1 - d3").WithArguments("-", "C<dynamic>.A<object, object>", "C<dynamic>.A<object, dynamic>"),
                // (24,20): error CS0034: Operator '-' is ambiguous on operands of type 'C<dynamic>.A<object, object>' and 'C<object>.A<dynamic, object>'
                Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "d1 - d4").WithArguments("-", "C<dynamic>.A<object, object>", "C<object>.A<dynamic, object>"),
                // (25,20): error CS0034: Operator '-' is ambiguous on operands of type 'C<object>.A<object, object>' and 'C<dynamic>.A<object, dynamic>'
                Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "d2 - d3").WithArguments("-", "C<object>.A<object, object>", "C<dynamic>.A<object, dynamic>"));
        }

        [Fact]
        [WorkItem(624270, "DevDiv"), WorkItem(624274, "DevDiv")]
        public void TestBinaryOperatorOverloading_Delegates_Dynamic_Ambiguous_Inference()
        {
            string source = @"
using System;
 
class Program
{
    static void Main()
    {
        Action<object> a = null;
        Foo(c => c == a);
    }
 
    static void Foo(Func<Action<object>, IComparable> x) { }
    static void Foo(Func<Action<dynamic>, IConvertible> x) { }
}
";
            // Dev11 considers Action<object> == Action<dynamic> ambiguous and thus chooses Foo(Func<Action<object>, IComparable>) overload.

            CreateCompilationWithMscorlibAndSystemCore(source).VerifyDiagnostics(
                // (9,9): error CS0121: The call is ambiguous between the following methods or properties: 'Program.Foo(System.Func<System.Action<object>, System.IComparable>)' and 'Program.Foo(System.Func<System.Action<dynamic>, System.IConvertible>)'
                Diagnostic(ErrorCode.ERR_AmbigCall, "Foo").WithArguments("Program.Foo(System.Func<System.Action<object>, System.IComparable>)", "Program.Foo(System.Func<System.Action<dynamic>, System.IConvertible>)"));
        }

        [Fact]
        public void TestBinaryOperatorOverloading_Pointers_Dynamic()
        {
            string source = @"
#pragma warning disable 219 // The variable is assigned but its value is never used

using System.Collections.Generic;

unsafe class C<T>
{    
    enum E { A }

    public void M()
    {
        var o = C<object>.E.A;
        var d = C<dynamic>.E.A;
        var dict1 = C<Dictionary<object, dynamic>>.E.A;
        var dict2 = C<Dictionary<dynamic, object>>.E.A;

        var eq1 = &o == &d;
        var eq2 = &d == &o;
        var eq3 = &dict1 == &dict2;
        var eq4 = &dict2 == &dict1;

        var neq1 = &o != &d;
        var neq2 = &d != &o;
        var neq3 = &dict1 != &dict2;
        var neq4 = &dict2 != &dict1;

        var sub1 = &o - &d;
        var sub2 = &d - &o;
        var sub3 = &dict1 - &dict2;
        var sub4 = &dict2 - &dict1;

        var subi1 = &o - 1;
        var subi2 = &d - 1;
        var subi3 = &dict1 - 1;
        var subi4 = &dict2 - 1;

        var addi1 = &o + 1;
        var addi2 = &d + 1;
        var addi3 = &dict1 + 1;
        var addi4 = &dict2 + 1;

        var iadd1 = 1 + &o;
        var iadd2 = 1 + &d;
        var iadd3 = 1 + &dict1;
        var iadd4 = 1 + &dict2;
    }      
}
";
            // Dev11 reports "error CS0034: Operator '-' is ambiguous on operands ... and ..." for all ptr - ptr
            CreateCompilationWithMscorlibAndSystemCore(source, options: TestOptions.UnsafeReleaseDll).VerifyDiagnostics();
        }

        [Fact]
        public void TestOverloadResolutionTiebreakers()
        {
            string source = @"
using System;
struct S
{
    public static bool operator == (S x, S y) { return true; }
    public static bool operator != (S x, S y) { return false; }
    public static bool operator == (S? x, S? y) { return true; }
    public static bool operator != (S? x, S? y) { return false; }
    public override bool Equals(object s) { return true; }
    public override int GetHashCode() { return 0; }
    public override string ToString() { return this.str; }
}

class X<T> 
{
    public static int operator +(X<T> x, int y) { return 0; }
    public static int operator +(X<T> x, T y) { return 0; }
}

struct Q<U> where U : struct
{
    public static int operator +(Q<U> x, int y) { return 0; }
    public static int? operator +(Q<U>? x, U? y) { return 1; }
}


class C
{
    static void M()
    {
        S s1 = new S();
        S s2 = new S();
        S? s3 = new S();
        S? s4 = null;
        X<int> xint = null;

        int x = xint + 123; //-UserDefinedAddition

        // In this case the native compiler and the spec disagree. Roslyn implements the spec.
        // The tiebreaker is supposed to check for *specificity* first, and then *liftedness*.
        // The native compiler eliminates the lifted operator even if it is more specific:

        int? q = new Q<int>?() + new int?(); //-LiftedUserDefinedAddition

        // All of these go to a user-defined equality operator;
        // the lifted form is always worse than the unlifted form,
        // and the user-defined form is always better than turning
        // '== null' into a call to HasValue().
        bool[] b = 
        {
            s1 == s2,      //-UserDefinedEqual
            s1 == s3,      //-UserDefinedEqual
            s1 == null,    //-UserDefinedEqual
            s3 == s1,      //-UserDefinedEqual
            s3 == s4,      //-UserDefinedEqual
            s3 == null,    //-UserDefinedEqual
            null == s1,    //-UserDefinedEqual
            null == s3     //-UserDefinedEqual
        };
        

    }
}";
            TestOperatorKinds(source);
        }

        [Fact]
        public void TestUserDefinedCompoundAssignment()
        {
            string source = @"
using System;
struct S
{
    private string str;
    public S(char chr) { this.str = chr.ToString(); }
    public S(string str) { this.str = str; }
    public static S operator + (S x, S y) { return new S('(' + x.str + '+' + y.str + ')'); }
    public static S operator - (S x, S y) { return new S('(' + x.str + '-' + y.str + ')'); }
    public static S operator % (S x, S y) { return new S('(' + x.str + '%' + y.str + ')'); }
    public static S operator / (S x, S y) { return new S('(' + x.str + '/' + y.str + ')'); }
    public static S operator * (S x, S y) { return new S('(' + x.str + '*' + y.str + ')'); }
    public static S operator & (S x, S y) { return new S('(' + x.str + '&' + y.str + ')'); }
    public static S operator | (S x, S y) { return new S('(' + x.str + '|' + y.str + ')'); }
    public static S operator ^ (S x, S y) { return new S('(' + x.str + '^' + y.str + ')'); }
    public static S operator << (S x, int y) { return new S('(' + x.str + '<' + '<' + y.ToString() + ')'); }
    public static S operator >> (S x, int y) { return new S('(' + x.str + '>' + '>' + y.ToString() + ')'); }
    public override string ToString() { return this.str; }
}

class C
{
    static void Main()
    {
        S a = new S('a');
        S b = new S('b'); 
        S c = new S('c'); 
        S d = new S('d'); 
        S e = new S('e'); 
        S f = new S('f'); 
        S g = new S('g'); 
        S h = new S('h');
        S i = new S('i');
        a += b;
        a -= c;
        a *= d;
        a /= e;
        a %= f;
        a <<= 10;
        a >>= 20;
        a &= g;
        a |= h;
        a ^= i;
        Console.WriteLine(a);
    }
}";
            string output = @"((((((((((a+b)-c)*d)/e)%f)<<10)>>20)&g)|h)^i)";

            CompileAndVerify(source: source, expectedOutput: output);
        }

        [Fact]
        public void TestUserDefinedBinaryOperatorOverloadResolution()
        {
            TestOperatorKinds(@"
using System;
struct S
{
    public static int operator + (S x1, S x2) { return 1; }
    public static int? operator - (S x1, S? x2) { return 1; }
    public static S operator & (S x1, S x2) { return x1; }
    public static bool operator true(S? x1) { return true; }
    public static bool operator false(S? x1) { return false; }
  
}

class B
{
    public static bool operator ==(B b1, B b2) { return true; }
    public static bool operator !=(B b1, B b2) { return true; }
}

class D : B {}

class C
{
    static void M()
    {
        bool f;
        B b = null;
        D d = null;
        S s1 = new S();
        S? s2 = s1;
        int i1;
        int? i2;
        
        i1 = s1 + s1; //-UserDefinedAddition
        i2 = s1 + s2; //-LiftedUserDefinedAddition
        i2 = s2 + s1; //-LiftedUserDefinedAddition
        i2 = s2 + s2; //-LiftedUserDefinedAddition

        // No lifted form.
        i2 = s1 - s1; //-UserDefinedSubtraction
        i2 = s1 - s2; //-UserDefinedSubtraction

        f = b == b; //-UserDefinedEqual
        f = b == d; //-UserDefinedEqual
        f = d == b; //-UserDefinedEqual
        f = d == d; //-UserDefinedEqual

        s1 = s1 & s1; //-UserDefinedAnd
        s2 = s2 & s1; //-LiftedUserDefinedAnd
        s2 = s1 & s2; //-LiftedUserDefinedAnd
        s2 = s2 & s2; //-LiftedUserDefinedAnd

        // No lifted form.
        s1 = s1 && s1; //-LogicalUserDefinedAnd

// UNDONE: More tests

    }
}");
        }

        [Fact]
        public void TestUserDefinedUnaryOperatorOverloadResolution()
        {
            TestOperatorKinds(@"
using System;
struct S
{
    public static int operator +(S s) { return 1; }
    public static int operator -(S? s) { return 2; }
    public static int operator !(S s) { return 3; }
    public static int operator ~(S s) { return 4; }
    public static S operator ++(S s) { return s; }
    public static S operator --(S? s) { return (S)s; }
}

class C
{
    static void M()
    {
        S s1 = new S();
        S? s2 = s1;
        int i1;
        int? i2;
        
        i1 = +s1; //-UserDefinedUnaryPlus
        i2 = +s2; //-LiftedUserDefinedUnaryPlus

        // No lifted form.
        i1 = -s1; //-UserDefinedUnaryMinus
        i1 = -s2; //-UserDefinedUnaryMinus   
        
        i1 = !s1; //-UserDefinedLogicalNegation
        i2 = !s2; //-LiftedUserDefinedLogicalNegation
 
        i1 = ~s1; //-UserDefinedBitwiseComplement
        i2 = ~s2; //-LiftedUserDefinedBitwiseComplement

        s1++; //-UserDefinedPostfixIncrement
        s2++; //-LiftedUserDefinedPostfixIncrement

        ++s1; //-UserDefinedPrefixIncrement
        ++s2; //-LiftedUserDefinedPrefixIncrement

        // No lifted form
        s1--; //-UserDefinedPostfixDecrement
        s2--; //-UserDefinedPostfixDecrement
    }
}");
        }

        [Fact]
        public void TestUnaryOperatorOverloadingErrors()
        {
            TestErrors(@"
class C 
{ 
// UNDONE: Write tests for the rest of them
    void M() 
    { 
        if(!1) {}
    }
}
",
"'!1' error CS0023: Operator '!' cannot be applied to operand of type 'int'");
        }

        [Fact]
        public void TestBinaryOperatorOverloadingErrors()
        {
            // The native compiler and Roslyn report slightly different errors here.
            // The native compiler reports CS0019 when attempting to add or compare long and ulong:
            // that is "operator cannot be applied to operands of type long and ulong". This is
            // correct but not as specific as it could be; the error is actually because overload
            // resolution is ambiguous. The double + double --> double, float + float --> float
            // and decimal + decimal --> decimal operators are all applicable but overload resolution
            // finds that this set of applicable operators is ambiguous; float is better than double,
            // but neither float nor decimal is better than the other.
            //
            // Roslyn produces the more accurate error; this is an ambiguity.
            //
            // Comparing string and exception is not ambiguous; the only applicable operator
            // is the reference equality operator, and it requires that its operand types be 
            // convertible to each other.

            string source = @"
class C 
{ 
    bool N() { return false; }
    void M() 
    { 
        long i64 = 1;
        ulong ui64 = 1;
        System.String s1 = null;
        System.Exception ex1 = null;
        object o1 = i64 + ui64; // CS0034
        bool b1 = i64 == ui64;  // CS0034
        bool b2 = s1 == ex1;    // CS0019
        bool b3 = (object)s1 == ex1; // legal!
    }
}";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
// (11,21): error CS0034: Operator '+' is ambiguous on operands of type 'long' and 'ulong'
//         object o1 = i64 + ui64; // CS0034
Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "i64 + ui64").WithArguments("+", "long", "ulong"),
// (12,19): error CS0034: Operator '==' is ambiguous on operands of type 'long' and 'ulong'
//         bool b1 = i64 == ui64;  // CS0034
Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "i64 == ui64").WithArguments("==", "long", "ulong"),
// (13,19): error CS0019: Operator '==' cannot be applied to operands of type 'string' and 'System.Exception'
//         bool b2 = s1 == ex1;    // CS0019
Diagnostic(ErrorCode.ERR_BadBinaryOps, "s1 == ex1").WithArguments("==", "string", "System.Exception"));
        }

        [Fact]
        public void TestCompoundOperatorErrors()
        {
            TestErrors(@"
class C 
{ 
    // UNDONE: Add more error cases

    class D : C {}

    public static C operator + (C c1, C c2) { return c1; }

    public int ReadOnly { get { return 0; } }
    public int WriteOnly { set { } }
    void M()
    {
        C c = new C();
        D d = new D();

        c.ReadOnly += 1;
        c.WriteOnly += 1;
        
        int i32 = 1;
        long i64 = 1;
        
        // If we have x += y and the + is a built-in operator then
        // the result must be *explicitly* convertible to x, and y
        // must be *implicitly* convertible to x.  
        //
        // If the + is a user-defined operator then the result must
        // be *implicitly* convertible to x, and y need not have
        // any relationship with x.

        // Overload resolution resolves this as long + long --> long.
        // The result is explicitly convertible to int, but the right-hand
        // side is not, so this is an error.
        i32 += i64;

        // In the user-defined conversion, the result of the addition must 
        // be *implicitly* convertible to the left hand side:

        d += c;


    }
}",
"'c.ReadOnly' error CS0200: Property or indexer 'C.ReadOnly' cannot be assigned to -- it is read only",
"'c.WriteOnly' error CS0154: The property or indexer 'C.WriteOnly' cannot be used in this context because it lacks the get accessor",
"'i32 += i64' error CS0266: Cannot implicitly convert type 'long' to 'int'. An explicit conversion exists (are you missing a cast?)",
"'d += c' error CS0266: Cannot implicitly convert type 'C' to 'C.D'. An explicit conversion exists (are you missing a cast?)");

        }

        [Fact]
        public void TestOperatorOverloadResolution()
        {
            // UNDONE: User-defined operators

            // UNDONE: TestOverloadResolution(GenerateTest(PostfixIncrementTemplate, "++", "PostfixIncrement"));
            // UNDONE: TestOverloadResolution(GenerateTest(PostfixIncrementTemplate, "--", "PostfixDecrement"));
            TestOperatorKinds(GenerateTest(PrefixIncrementTemplate, "++", "PrefixIncrement"));
            TestOperatorKinds(GenerateTest(PrefixIncrementTemplate, "--", "PrefixDecrement"));
            // UNDONE: Pointer ++ --
            TestOperatorKinds(UnaryPlus);
            TestOperatorKinds(UnaryMinus);
            TestOperatorKinds(LogicalNegation);
            TestOperatorKinds(BitwiseComplement);
            TestOperatorKinds(EnumAddition);
            TestOperatorKinds(StringAddition);
            TestOperatorKinds(DelegateAddition);
            // UNDONE: Pointer addition
            TestOperatorKinds(EnumSubtraction);
            TestOperatorKinds(DelegateSubtraction);
            // UNDONE: Pointer subtraction
            TestOperatorKinds(GenerateTest(ArithmeticTemplate, "+", "Addition"));
            TestOperatorKinds(GenerateTest(ArithmeticTemplate, "-", "Subtraction"));
            TestOperatorKinds(GenerateTest(ArithmeticTemplate, "*", "Multiplication"));
            TestOperatorKinds(GenerateTest(ArithmeticTemplate, "/", "Division"));
            TestOperatorKinds(GenerateTest(ArithmeticTemplate, "%", "Remainder"));
            TestOperatorKinds(GenerateTest(ShiftTemplate, "<<", "LeftShift"));
            TestOperatorKinds(GenerateTest(ShiftTemplate, ">>", "RightShift"));
            TestOperatorKinds(GenerateTest(ArithmeticTemplate, "==", "Equal"));
            TestOperatorKinds(GenerateTest(ArithmeticTemplate, "!=", "NotEqual"));
            TestOperatorKinds(GenerateTest(EqualityTemplate, "!=", "NotEqual"));
            TestOperatorKinds(GenerateTest(EqualityTemplate, "!=", "NotEqual"));
            // UNDONE: Pointer equality
            TestOperatorKinds(GenerateTest(ComparisonTemplate, ">", "GreaterThan"));
            TestOperatorKinds(GenerateTest(ComparisonTemplate, ">=", "GreaterThanOrEqual"));
            TestOperatorKinds(GenerateTest(ComparisonTemplate, "<", "LessThan"));
            TestOperatorKinds(GenerateTest(ComparisonTemplate, "<=", "LessThanOrEqual"));
            TestOperatorKinds(GenerateTest(LogicTemplate, "^", "Xor"));
            TestOperatorKinds(GenerateTest(LogicTemplate, "&", "And"));
            TestOperatorKinds(GenerateTest(LogicTemplate, "|", "Or"));
            TestOperatorKinds(GenerateTest(ShortCircuitTemplate, "&&", "And"));
            TestOperatorKinds(GenerateTest(ShortCircuitTemplate, "||", "Or"));
        }

        [Fact]
        public void TestEnumOperatorOverloadResolution()
        {
            TestOperatorKinds(GenerateTest(EnumLogicTemplate, "^", "Xor"));
            TestOperatorKinds(GenerateTest(EnumLogicTemplate, "&", "And"));
            TestOperatorKinds(GenerateTest(EnumLogicTemplate, "|", "Or"));
        }

        [Fact]
        public void TestConstantOperatorOverloadResolution()
        {
            string code =
@"class C
{
    static void F(object o) { }
    static void M()
    {
        const short ci16 = 1;
        uint u32 = 1;
        F(u32 + 1); //-UIntAddition
        F(2 + u32); //-UIntAddition
        F(u32 + ci16); //-LongAddition
        F(u32 + int.MaxValue); //-UIntAddition
        F(u32 + (-1)); //-LongAddition
        //-IntUnaryMinus
        F(u32 + long.MaxValue); //-LongAddition
        int i32 = 2;
        F(i32 + 1); //-IntAddition
        F(2 + i32); //-IntAddition
        F(i32 + ci16); //-IntAddition
        F(i32 + int.MaxValue); //-IntAddition
        F(i32 + (-1)); //-IntAddition
        //-IntUnaryMinus
    }
}
";
            TestOperatorKinds(code);
        }

        private void TestBoundTree(string source, System.Func<IEnumerable<KeyValuePair<TreeDumperNode, TreeDumperNode>>, IEnumerable<string>> query)
        {
            // The mechanism of this test is: we build the bound tree for the code passed in and then extract
            // from it the nodes that describe the operators. We then compare the description of
            // the operators given to the comment that follows the use of the operator.

            var compilation = CreateCompilationWithMscorlib(source, options: TestOptions.ReleaseDll);
            var method = (SourceMethodSymbol)compilation.GlobalNamespace.GetTypeMembers("C").Single().GetMembers("M").Single();
            var diagnostics = new DiagnosticBag();
            var block = MethodCompiler.BindMethodBody(method, new TypeCompilationState(method.ContainingType, compilation, null), diagnostics);
            var tree = BoundTreeDumperNodeProducer.MakeTree(block);
            var results = string.Join("\n",
                query(tree.PreorderTraversal())
                .ToArray());

            var expected = string.Join("\n", source
                .Split(new[] { "\r\n" }, System.StringSplitOptions.RemoveEmptyEntries)
                .Where(x => x.Contains("//-"))
                .Select(x => x.Substring(x.IndexOf("//-") + 3).Trim())
                .ToArray());

            Assert.Equal(expected, results);

        }

        private void TestOperatorKinds(string source)
        {
            // The mechanism of this test is: we build the bound tree for the code passed in and then extract
            // from it the nodes that describe the operators. We then compare the description of
            // the operators given to the comment that follows the use of the operator.

            TestBoundTree(source, edges =>
                from edge in edges
                let node = edge.Value
                where node.Text == "operatorKind"
                where node.Value != null
                select node.Value.ToString());
        }

        private void TestCompoundAssignment(string source)
        {
            TestBoundTree(source, edges =>
                from edge in edges
                let node = edge.Value
                where node != null && (node.Text == "eventAssignmentOperator" || node.Text == "compoundAssignmentOperator")
                select string.Join(" ", from child in node.Children
                                        where child.Text == "@operator" ||
                                              child.Text == "isAddition" ||
                                              child.Text == "isDynamic" ||
                                              child.Text == "leftConversion" ||
                                              child.Text == "finalConversion"
                                        select child.Text + ": " + (child.Text == "@operator" ? ((BinaryOperatorSignature)child.Value).Kind.ToString() : child.Value.ToString())));
        }

        private void TestTypes(string source)
        {
            TestBoundTree(source, edges =>
                from edge in edges
                let node = edge.Value
                where node.Text == "type"
                select edge.Key.Text + ": " + (node.Value != null ? node.Value.ToString() : "<null>"));
        }

        private static string FormatTypeArgumentList(ImmutableArray<TypeSymbol>? arguments)
        {
            if (arguments == null || arguments.Value.IsEmpty)
            {
                return "";
            }

            string s = "<";
            for (int i = 0; i < arguments.Value.Length; ++i)
            {
                if (i != 0)
                {
                    s += ", ";
                }
                s += arguments.Value[i].ToString();
            }

            return s + ">";
        }

        private void TestDynamicMemberAccess(string source)
        {
            TestBoundTree(source, edges =>
                from edge in edges
                let node = edge.Value
                where node.Text == "dynamicMemberAccess"
                let name = node["name"]
                let typeArguments = node["typeArgumentsOpt"].Value as ImmutableArray<TypeSymbol>?
                select name.Value.ToString() + FormatTypeArgumentList(typeArguments));
        }

        static string GenerateTest(string template, string op, string opkind)
        {
            string result = template.Replace("OPERATOR", op);
            result = result.Replace("KIND", opkind);
            return result;
        }

        #region "Constant String"
        const string Prefix = @"
class C 
{ 
    enum E { }
    void N(params object[] p) {}
    delegate void D();
    void M() 
    { 
        E e1;
        E e2; 

        string s1 = null;
        string s2 = null;
        object o1 = null;
        object o2 = null;

        bool bln;
        bool? nbln;
        D d1 = null;
        D d2 = null;
        int i = 1;
        E e = 0;
        int? ni = 1;
        E? ne = 0;
        int i1 = 0;
        char chr; 
        sbyte i08 = 1;
        short i16 = 1;
        int i32 = 1;
        long i64 = 1;
        byte u08 = 1;
        ushort u16 = 1;
        uint u32 = 1;
        ulong u64 = 1;
        float r32 = 1;
        double r64 = 1;
        decimal dec; // UNDONE: Decimal constants not supported yet.
        char? nchr = null;
        sbyte? ni08 = 1;
        short? ni16 = 1;
        int? ni32 = 1;
        long? ni64 = 1;
        byte? nu08 = 1;
        ushort? nu16 = 1;
        uint? nu32 = 1;
        ulong? nu64 = 1;
        float? nr32 = 1;
        double? nr64 = 1;
        decimal? ndec; // UNDONE: Decimal constants not supported yet.

        N(
";

        const string Postfix = @"
        );
    }
}
";

        const string EnumAddition = Prefix + @"
i + e,          //-UnderlyingAndEnumAddition
i + ne,         //-LiftedUnderlyingAndEnumAddition
e + i,          //-EnumAndUnderlyingAddition
e + ni,         //-LiftedEnumAndUnderlyingAddition
ni + e,         //-LiftedUnderlyingAndEnumAddition
ni + ne,        //-LiftedUnderlyingAndEnumAddition
ne + i,         //-LiftedEnumAndUnderlyingAddition
ne + ni         //-LiftedEnumAndUnderlyingAddition" + Postfix;

        const string DelegateAddition = Prefix + @"
        d1 + d2 //-DelegateCombination" + Postfix;

        const string StringAddition = Prefix + @"
        s1 + s1, //-StringConcatenation
        s1 + o1, //-StringAndObjectConcatenation
        i1 + s1  //-ObjectAndStringConcatenation" + Postfix;

        const string ArithmeticTemplate = Prefix + @"
chr OPERATOR chr,                   //-IntKIND
chr OPERATOR i16,                   //-IntKIND
chr OPERATOR i32,                   //-IntKIND
chr OPERATOR i64,                   //-LongKIND
chr OPERATOR u16,                   //-IntKIND
chr OPERATOR u32,                   //-UIntKIND
chr OPERATOR u64,                   //-ULongKIND
chr OPERATOR r32,                   //-FloatKIND
chr OPERATOR r64,                   //-DoubleKIND
chr OPERATOR dec,                   //-DecimalKIND
chr OPERATOR nchr,                  //-LiftedIntKIND
chr OPERATOR ni16,                  //-LiftedIntKIND
chr OPERATOR ni32,                  //-LiftedIntKIND
chr OPERATOR ni64,                  //-LiftedLongKIND
chr OPERATOR nu16,                  //-LiftedIntKIND
chr OPERATOR nu32,                  //-LiftedUIntKIND
chr OPERATOR nu64,                  //-LiftedULongKIND
chr OPERATOR nr32,                  //-LiftedFloatKIND
chr OPERATOR nr64,                  //-LiftedDoubleKIND
chr OPERATOR ndec,                  //-LiftedDecimalKIND

i16 OPERATOR chr,                   //-IntKIND
i16 OPERATOR i16,                   //-IntKIND
i16 OPERATOR i32,                   //-IntKIND
i16 OPERATOR i64,                   //-LongKIND
i16 OPERATOR u16,                   //-IntKIND
i16 OPERATOR u32,                   //-LongKIND
// i16 OPERATOR u64, (ambiguous)
i16 OPERATOR r32,                   //-FloatKIND
i16 OPERATOR r64,                   //-DoubleKIND
i16 OPERATOR dec,                   //-DecimalKIND
i16 OPERATOR nchr,                   //-LiftedIntKIND
i16 OPERATOR ni16,                  //-LiftedIntKIND
i16 OPERATOR ni32,                  //-LiftedIntKIND
i16 OPERATOR ni64,                  //-LiftedLongKIND
i16 OPERATOR nu16,                  //-LiftedIntKIND
i16 OPERATOR nu32,                  //-LiftedLongKIND
// i16 OPERATOR nu64, (ambiguous)
i16 OPERATOR nr32,                  //-LiftedFloatKIND
i16 OPERATOR nr64,                  //-LiftedDoubleKIND
i16 OPERATOR ndec,                  //-LiftedDecimalKIND

i32 OPERATOR chr,                   //-IntKIND
i32 OPERATOR i16,                   //-IntKIND
i32 OPERATOR i32,                   //-IntKIND
i32 OPERATOR i64,                   //-LongKIND
i32 OPERATOR u16,                   //-IntKIND
i32 OPERATOR u32,                   //-LongKIND
// i32 OPERATOR u64, (ambiguous)
i32 OPERATOR r32,                   //-FloatKIND
i32 OPERATOR r64,                   //-DoubleKIND
i32 OPERATOR dec,                   //-DecimalKIND
i32 OPERATOR nchr,                  //-LiftedIntKIND
i32 OPERATOR ni16,                  //-LiftedIntKIND
i32 OPERATOR ni32,                  //-LiftedIntKIND
i32 OPERATOR ni64,                  //-LiftedLongKIND
i32 OPERATOR nu16,                  //-LiftedIntKIND
i32 OPERATOR nu32,                  //-LiftedLongKIND
// i32 OPERATOR nu64, (ambiguous)
i32 OPERATOR nr32,                  //-LiftedFloatKIND
i32 OPERATOR nr64,                  //-LiftedDoubleKIND
i32 OPERATOR ndec,                  //-LiftedDecimalKIND

i64 OPERATOR chr,                   //-LongKIND
i64 OPERATOR i16,                   //-LongKIND
i64 OPERATOR i32,                   //-LongKIND
i64 OPERATOR i64,                   //-LongKIND
i64 OPERATOR u16,                   //-LongKIND
i64 OPERATOR u32,                   //-LongKIND
// i64 OPERATOR u64, (ambiguous)
i64 OPERATOR r32,                   //-FloatKIND
i64 OPERATOR r64,                   //-DoubleKIND
i64 OPERATOR dec,                   //-DecimalKIND
i64 OPERATOR nchr,                  //-LiftedLongKIND
i64 OPERATOR ni16,                  //-LiftedLongKIND
i64 OPERATOR ni32,                  //-LiftedLongKIND
i64 OPERATOR ni64,                  //-LiftedLongKIND
i64 OPERATOR nu16,                  //-LiftedLongKIND
i64 OPERATOR nu32,                  //-LiftedLongKIND
// i64 OPERATOR nu64, (ambiguous)
i64 OPERATOR nr32,                  //-LiftedFloatKIND
i64 OPERATOR nr64,                  //-LiftedDoubleKIND
i64 OPERATOR ndec,                  //-LiftedDecimalKIND

u16 OPERATOR chr,                   //-IntKIND
u16 OPERATOR i16,                   //-IntKIND
u16 OPERATOR i32,                   //-IntKIND
u16 OPERATOR i64,                   //-LongKIND
u16 OPERATOR u16,                   //-IntKIND
u16 OPERATOR u32,                   //-UIntKIND
u16 OPERATOR u64,                   //-ULongKIND
u16 OPERATOR r32,                   //-FloatKIND
u16 OPERATOR r64,                   //-DoubleKIND
u16 OPERATOR dec,                   //-DecimalKIND
u16 OPERATOR nchr,                  //-LiftedIntKIND
u16 OPERATOR ni16,                  //-LiftedIntKIND
u16 OPERATOR ni32,                  //-LiftedIntKIND
u16 OPERATOR ni64,                  //-LiftedLongKIND
u16 OPERATOR nu16,                  //-LiftedIntKIND
u16 OPERATOR nu32,                  //-LiftedUIntKIND
u16 OPERATOR nu64,                  //-LiftedULongKIND
u16 OPERATOR nr32,                  //-LiftedFloatKIND
u16 OPERATOR nr64,                  //-LiftedDoubleKIND
u16 OPERATOR ndec,                  //-LiftedDecimalKIND

u32 OPERATOR chr,                   //-UIntKIND
u32 OPERATOR i16,                   //-LongKIND
u32 OPERATOR i32,                   //-LongKIND
u32 OPERATOR i64,                   //-LongKIND
u32 OPERATOR u16,                   //-UIntKIND
u32 OPERATOR u32,                   //-UIntKIND
u32 OPERATOR u64,                   //-ULongKIND
u32 OPERATOR r32,                   //-FloatKIND
u32 OPERATOR r64,                   //-DoubleKIND
u32 OPERATOR dec,                   //-DecimalKIND
u32 OPERATOR nchr,                  //-LiftedUIntKIND
u32 OPERATOR ni16,                  //-LiftedLongKIND
u32 OPERATOR ni32,                  //-LiftedLongKIND
u32 OPERATOR ni64,                  //-LiftedLongKIND
u32 OPERATOR nu16,                  //-LiftedUIntKIND
u32 OPERATOR nu32,                  //-LiftedUIntKIND
u32 OPERATOR nu64,                  //-LiftedULongKIND
u32 OPERATOR nr32,                  //-LiftedFloatKIND
u32 OPERATOR nr64,                  //-LiftedDoubleKIND
u32 OPERATOR ndec,                  //-LiftedDecimalKIND

u64 OPERATOR chr,                   //-ULongKIND
// u64 OPERATOR i16, (ambiguous)
// u64 OPERATOR i32, (ambiguous)
// u64 OPERATOR i64, (ambiguous)
u64 OPERATOR u16,                   //-ULongKIND
u64 OPERATOR u32,                   //-ULongKIND
u64 OPERATOR u64,                   //-ULongKIND
u64 OPERATOR r32,                   //-FloatKIND
u64 OPERATOR r64,                   //-DoubleKIND
u64 OPERATOR dec,                   //-DecimalKIND
u64 OPERATOR nchr,                   //-LiftedULongKIND
// u64 OPERATOR ni16, (ambiguous)
// u64 OPERATOR ni32, (ambiguous)
// u64 OPERATOR ni64, (ambiguous)
u64 OPERATOR nu16,                  //-LiftedULongKIND
u64 OPERATOR nu32,                  //-LiftedULongKIND
u64 OPERATOR nu64,                  //-LiftedULongKIND
u64 OPERATOR nr32,                  //-LiftedFloatKIND
u64 OPERATOR nr64,                  //-LiftedDoubleKIND
u64 OPERATOR ndec,                  //-LiftedDecimalKIND

r32 OPERATOR chr,                   //-FloatKIND
r32 OPERATOR i16,                   //-FloatKIND
r32 OPERATOR i32,                   //-FloatKIND
r32 OPERATOR i64,                   //-FloatKIND
r32 OPERATOR u16,                   //-FloatKIND
r32 OPERATOR u32,                   //-FloatKIND
r32 OPERATOR u64,                   //-FloatKIND
r32 OPERATOR r32,                   //-FloatKIND
r32 OPERATOR r64,                   //-DoubleKIND
// r32 OPERATOR dec, (none applicable)
r32 OPERATOR nchr,                  //-LiftedFloatKIND
r32 OPERATOR ni16,                  //-LiftedFloatKIND
r32 OPERATOR ni32,                  //-LiftedFloatKIND
r32 OPERATOR ni64,                  //-LiftedFloatKIND
r32 OPERATOR nu16,                  //-LiftedFloatKIND
r32 OPERATOR nu32,                  //-LiftedFloatKIND
r32 OPERATOR nu64,                  //-LiftedFloatKIND
r32 OPERATOR nr32,                  //-LiftedFloatKIND
r32 OPERATOR nr64,                  //-LiftedDoubleKIND
// r32 OPERATOR ndec, (none applicable)

r64 OPERATOR chr,                   //-DoubleKIND
r64 OPERATOR i16,                   //-DoubleKIND
r64 OPERATOR i32,                   //-DoubleKIND
r64 OPERATOR i64,                   //-DoubleKIND
r64 OPERATOR u16,                   //-DoubleKIND
r64 OPERATOR u32,                   //-DoubleKIND
r64 OPERATOR u64,                   //-DoubleKIND
r64 OPERATOR r32,                   //-DoubleKIND
r64 OPERATOR r64,                   //-DoubleKIND
// r64 OPERATOR dec, (none applicable)
r64 OPERATOR nchr,                  //-LiftedDoubleKIND
r64 OPERATOR ni16,                  //-LiftedDoubleKIND
r64 OPERATOR ni32,                  //-LiftedDoubleKIND
r64 OPERATOR ni64,                  //-LiftedDoubleKIND
r64 OPERATOR nu16,                  //-LiftedDoubleKIND
r64 OPERATOR nu32,                  //-LiftedDoubleKIND
r64 OPERATOR nu64,                  //-LiftedDoubleKIND
r64 OPERATOR nr32,                  //-LiftedDoubleKIND
r64 OPERATOR nr64,                  //-LiftedDoubleKIND
// r64 OPERATOR ndec, (none applicable)

dec OPERATOR chr,                   //-DecimalKIND
dec OPERATOR i16,                   //-DecimalKIND
dec OPERATOR i32,                   //-DecimalKIND
dec OPERATOR i64,                   //-DecimalKIND
dec OPERATOR u16,                   //-DecimalKIND
dec OPERATOR u32,                   //-DecimalKIND
dec OPERATOR u64,                   //-DecimalKIND
// dec OPERATOR r32, (none applicable)
// dec OPERATOR r64, (none applicable)
dec OPERATOR dec,                   //-DecimalKIND
dec OPERATOR nchr,                  //-LiftedDecimalKIND
dec OPERATOR ni16,                  //-LiftedDecimalKIND
dec OPERATOR ni32,                  //-LiftedDecimalKIND
dec OPERATOR ni64,                  //-LiftedDecimalKIND
dec OPERATOR nu16,                  //-LiftedDecimalKIND
dec OPERATOR nu32,                  //-LiftedDecimalKIND
dec OPERATOR nu64,                  //-LiftedDecimalKIND
// dec OPERATOR nr32,   (none applicable)
// dec OPERATOR nr64,  (none applicable)
dec OPERATOR ndec,                   //-LiftedDecimalKIND

nchr OPERATOR chr,                   //-LiftedIntKIND
nchr OPERATOR i16,                   //-LiftedIntKIND
nchr OPERATOR i32,                   //-LiftedIntKIND
nchr OPERATOR i64,                   //-LiftedLongKIND
nchr OPERATOR u16,                   //-LiftedIntKIND
nchr OPERATOR u32,                   //-LiftedUIntKIND
nchr OPERATOR u64,                   //-LiftedULongKIND
nchr OPERATOR r32,                   //-LiftedFloatKIND
nchr OPERATOR r64,                   //-LiftedDoubleKIND
nchr OPERATOR dec,                   //-LiftedDecimalKIND
nchr OPERATOR nchr,                  //-LiftedIntKIND
nchr OPERATOR ni16,                  //-LiftedIntKIND
nchr OPERATOR ni32,                  //-LiftedIntKIND
nchr OPERATOR ni64,                  //-LiftedLongKIND
nchr OPERATOR nu16,                  //-LiftedIntKIND
nchr OPERATOR nu32,                  //-LiftedUIntKIND
nchr OPERATOR nu64,                  //-LiftedULongKIND
nchr OPERATOR nr32,                  //-LiftedFloatKIND
nchr OPERATOR nr64,                  //-LiftedDoubleKIND
nchr OPERATOR ndec,                  //-LiftedDecimalKIND

ni16 OPERATOR chr,                   //-LiftedIntKIND
ni16 OPERATOR i16,                   //-LiftedIntKIND
ni16 OPERATOR i32,                   //-LiftedIntKIND
ni16 OPERATOR i64,                   //-LiftedLongKIND
ni16 OPERATOR u16,                   //-LiftedIntKIND
ni16 OPERATOR u32,                   //-LiftedLongKIND
// ni16 OPERATOR u64, (ambiguous)
ni16 OPERATOR r32,                   //-LiftedFloatKIND
ni16 OPERATOR r64,                   //-LiftedDoubleKIND
ni16 OPERATOR dec,                   //-LiftedDecimalKIND
ni16 OPERATOR nchr,                   //-LiftedIntKIND
ni16 OPERATOR ni16,                  //-LiftedIntKIND
ni16 OPERATOR ni32,                  //-LiftedIntKIND
ni16 OPERATOR ni64,                  //-LiftedLongKIND
ni16 OPERATOR nu16,                  //-LiftedIntKIND
ni16 OPERATOR nu32,                  //-LiftedLongKIND
// ni16 OPERATOR nu64, (ambiguous)
ni16 OPERATOR nr32,                  //-LiftedFloatKIND
ni16 OPERATOR nr64,                  //-LiftedDoubleKIND
ni16 OPERATOR ndec,                  //-LiftedDecimalKIND

ni32 OPERATOR chr,                   //-LiftedIntKIND
ni32 OPERATOR i16,                   //-LiftedIntKIND
ni32 OPERATOR i32,                   //-LiftedIntKIND
ni32 OPERATOR i64,                   //-LiftedLongKIND
ni32 OPERATOR u16,                   //-LiftedIntKIND
ni32 OPERATOR u32,                   //-LiftedLongKIND
// ni32 OPERATOR u64, (ambiguous)
ni32 OPERATOR r32,                   //-LiftedFloatKIND
ni32 OPERATOR r64,                   //-LiftedDoubleKIND
ni32 OPERATOR dec,                   //-LiftedDecimalKIND
ni32 OPERATOR nchr,                   //-LiftedIntKIND
ni32 OPERATOR ni16,                  //-LiftedIntKIND
ni32 OPERATOR ni32,                  //-LiftedIntKIND
ni32 OPERATOR ni64,                  //-LiftedLongKIND
ni32 OPERATOR nu16,                  //-LiftedIntKIND
ni32 OPERATOR nu32,                  //-LiftedLongKIND
// ni32 OPERATOR nu64, (ambiguous)
ni32 OPERATOR nr32,                  //-LiftedFloatKIND
ni32 OPERATOR nr64,                  //-LiftedDoubleKIND
ni32 OPERATOR ndec,                  //-LiftedDecimalKIND

ni64 OPERATOR chr,                   //-LiftedLongKIND
ni64 OPERATOR i16,                   //-LiftedLongKIND
ni64 OPERATOR i32,                   //-LiftedLongKIND
ni64 OPERATOR i64,                   //-LiftedLongKIND
ni64 OPERATOR u16,                   //-LiftedLongKIND
ni64 OPERATOR u32,                   //-LiftedLongKIND
// ni64 OPERATOR u64, (ambiguous)
ni64 OPERATOR r32,                   //-LiftedFloatKIND
ni64 OPERATOR r64,                   //-LiftedDoubleKIND
ni64 OPERATOR dec,                   //-LiftedDecimalKIND
ni64 OPERATOR nchr,                   //-LiftedLongKIND
ni64 OPERATOR ni16,                  //-LiftedLongKIND
ni64 OPERATOR ni32,                  //-LiftedLongKIND
ni64 OPERATOR ni64,                  //-LiftedLongKIND
ni64 OPERATOR nu16,                  //-LiftedLongKIND
ni64 OPERATOR nu32,                  //-LiftedLongKIND
// ni64 OPERATOR nu64, (ambiguous)
ni64 OPERATOR nr32,                  //-LiftedFloatKIND
ni64 OPERATOR nr64,                  //-LiftedDoubleKIND
ni64 OPERATOR ndec,                  //-LiftedDecimalKIND

nu16 OPERATOR chr,                   //-LiftedIntKIND
nu16 OPERATOR i16,                   //-LiftedIntKIND
nu16 OPERATOR i32,                   //-LiftedIntKIND
nu16 OPERATOR i64,                   //-LiftedLongKIND
nu16 OPERATOR u16,                   //-LiftedIntKIND
nu16 OPERATOR u32,                   //-LiftedUIntKIND
nu16 OPERATOR u64,                   //-LiftedULongKIND
nu16 OPERATOR r32,                   //-LiftedFloatKIND
nu16 OPERATOR r64,                   //-LiftedDoubleKIND
nu16 OPERATOR dec,                   //-LiftedDecimalKIND
nu16 OPERATOR nchr,                   //-LiftedIntKIND
nu16 OPERATOR ni16,                  //-LiftedIntKIND
nu16 OPERATOR ni32,                  //-LiftedIntKIND
nu16 OPERATOR ni64,                  //-LiftedLongKIND
nu16 OPERATOR nu16,                  //-LiftedIntKIND
nu16 OPERATOR nu32,                  //-LiftedUIntKIND
nu16 OPERATOR nu64,                  //-LiftedULongKIND
nu16 OPERATOR nr32,                  //-LiftedFloatKIND
nu16 OPERATOR nr64,                  //-LiftedDoubleKIND
nu16 OPERATOR ndec,                  //-LiftedDecimalKIND

nu32 OPERATOR chr,                   //-LiftedUIntKIND
nu32 OPERATOR i16,                   //-LiftedLongKIND
nu32 OPERATOR i32,                   //-LiftedLongKIND
nu32 OPERATOR i64,                   //-LiftedLongKIND
nu32 OPERATOR u16,                   //-LiftedUIntKIND
nu32 OPERATOR u32,                   //-LiftedUIntKIND
nu32 OPERATOR u64,                   //-LiftedULongKIND
nu32 OPERATOR r32,                   //-LiftedFloatKIND
nu32 OPERATOR r64,                   //-LiftedDoubleKIND
nu32 OPERATOR dec,                   //-LiftedDecimalKIND
nu32 OPERATOR nchr,                   //-LiftedUIntKIND
nu32 OPERATOR ni16,                  //-LiftedLongKIND
nu32 OPERATOR ni32,                  //-LiftedLongKIND
nu32 OPERATOR ni64,                  //-LiftedLongKIND
nu32 OPERATOR nu16,                  //-LiftedUIntKIND
nu32 OPERATOR nu32,                  //-LiftedUIntKIND
nu32 OPERATOR nu64,                  //-LiftedULongKIND
nu32 OPERATOR nr32,                  //-LiftedFloatKIND
nu32 OPERATOR nr64,                  //-LiftedDoubleKIND
nu32 OPERATOR ndec,                  //-LiftedDecimalKIND

nu64 OPERATOR chr,                   //-LiftedULongKIND
// nu64 OPERATOR i16, (ambiguous)
// nu64 OPERATOR i32, (ambiguous)
// nu64 OPERATOR i64, (ambiguous)
nu64 OPERATOR u16,                   //-LiftedULongKIND
nu64 OPERATOR u32,                   //-LiftedULongKIND
nu64 OPERATOR u64,                   //-LiftedULongKIND
nu64 OPERATOR r32,                   //-LiftedFloatKIND
nu64 OPERATOR r64,                   //-LiftedDoubleKIND
nu64 OPERATOR dec,                   //-LiftedDecimalKIND
nu64 OPERATOR nchr,                   //-LiftedULongKIND
// nu64 OPERATOR ni16, (ambiguous)
// nu64 OPERATOR ni32, (ambiguous)
// nu64 OPERATOR ni64, (ambiguous)
nu64 OPERATOR nu16,                  //-LiftedULongKIND
nu64 OPERATOR nu32,                  //-LiftedULongKIND
nu64 OPERATOR nu64,                  //-LiftedULongKIND
nu64 OPERATOR nr32,                  //-LiftedFloatKIND
nu64 OPERATOR nr64,                  //-LiftedDoubleKIND
nu64 OPERATOR ndec,                  //-LiftedDecimalKIND

nr32 OPERATOR chr,                   //-LiftedFloatKIND
nr32 OPERATOR i16,                   //-LiftedFloatKIND
nr32 OPERATOR i32,                   //-LiftedFloatKIND
nr32 OPERATOR i64,                   //-LiftedFloatKIND
nr32 OPERATOR u16,                   //-LiftedFloatKIND
nr32 OPERATOR u32,                   //-LiftedFloatKIND
nr32 OPERATOR u64,                   //-LiftedFloatKIND
nr32 OPERATOR r32,                   //-LiftedFloatKIND
nr32 OPERATOR r64,                   //-LiftedDoubleKIND
// nr32 OPERATOR dec, (none applicable)
nr32 OPERATOR nchr,                   //-LiftedFloatKIND
nr32 OPERATOR ni16,                  //-LiftedFloatKIND
nr32 OPERATOR ni32,                  //-LiftedFloatKIND
nr32 OPERATOR ni64,                  //-LiftedFloatKIND
nr32 OPERATOR nu16,                  //-LiftedFloatKIND
nr32 OPERATOR nu32,                  //-LiftedFloatKIND
nr32 OPERATOR nu64,                  //-LiftedFloatKIND
nr32 OPERATOR nr32,                  //-LiftedFloatKIND
nr32 OPERATOR nr64,                  //-LiftedDoubleKIND
// nr32 OPERATOR ndec, (none applicable)

nr64 OPERATOR chr,                   //-LiftedDoubleKIND
nr64 OPERATOR i16,                   //-LiftedDoubleKIND
nr64 OPERATOR i32,                   //-LiftedDoubleKIND
nr64 OPERATOR i64,                   //-LiftedDoubleKIND
nr64 OPERATOR u16,                   //-LiftedDoubleKIND
nr64 OPERATOR u32,                   //-LiftedDoubleKIND
nr64 OPERATOR u64,                   //-LiftedDoubleKIND
nr64 OPERATOR r32,                   //-LiftedDoubleKIND
nr64 OPERATOR r64,                   //-LiftedDoubleKIND
// nr64 OPERATOR dec, (none applicable)
nr64 OPERATOR nchr,                   //-LiftedDoubleKIND
nr64 OPERATOR ni16,                  //-LiftedDoubleKIND
nr64 OPERATOR ni32,                  //-LiftedDoubleKIND
nr64 OPERATOR ni64,                  //-LiftedDoubleKIND
nr64 OPERATOR nu16,                  //-LiftedDoubleKIND
nr64 OPERATOR nu32,                  //-LiftedDoubleKIND
nr64 OPERATOR nu64,                  //-LiftedDoubleKIND
nr64 OPERATOR nr32,                  //-LiftedDoubleKIND
nr64 OPERATOR nr64,                  //-LiftedDoubleKIND
// nr64 OPERATOR ndec, (none applicable)

ndec OPERATOR chr,                   //-LiftedDecimalKIND
ndec OPERATOR i16,                   //-LiftedDecimalKIND
ndec OPERATOR i32,                   //-LiftedDecimalKIND
ndec OPERATOR i64,                   //-LiftedDecimalKIND
ndec OPERATOR u16,                   //-LiftedDecimalKIND
ndec OPERATOR u32,                   //-LiftedDecimalKIND
ndec OPERATOR u64,                   //-LiftedDecimalKIND
// ndec OPERATOR r32, (none applicable)
// ndec OPERATOR r64, (none applicable)
ndec OPERATOR dec,                   //-LiftedDecimalKIND
ndec OPERATOR nchr,                   //-LiftedDecimalKIND
ndec OPERATOR ni16,                  //-LiftedDecimalKIND
ndec OPERATOR ni32,                  //-LiftedDecimalKIND
ndec OPERATOR ni64,                  //-LiftedDecimalKIND
ndec OPERATOR nu16,                  //-LiftedDecimalKIND
ndec OPERATOR nu32,                  //-LiftedDecimalKIND
ndec OPERATOR nu64,                  //-LiftedDecimalKIND
// ndec OPERATOR nr32,   (none applicable)
// ndec OPERATOR nr64,  (none applicable)
ndec OPERATOR ndec                    //-LiftedDecimalKIND" + Postfix;

        const string EnumSubtraction = Prefix + @"
e - e,      //-EnumSubtraction
e - ne,     //-LiftedEnumSubtraction
e - i,      //-EnumAndUnderlyingSubtraction
e - ni,     //-LiftedEnumAndUnderlyingSubtraction
ne - e,     //-LiftedEnumSubtraction
ne - ne,    //-LiftedEnumSubtraction
ne - i,     //-LiftedEnumAndUnderlyingSubtraction
ne - ni     //-LiftedEnumAndUnderlyingSubtraction" + Postfix;

        const string DelegateSubtraction = Prefix + "d1 - d2 //-DelegateRemoval" + Postfix;

        const string ShiftTemplate = Prefix + @"
chr OPERATOR chr,                   //-IntKIND
chr OPERATOR i16,                   //-IntKIND
chr OPERATOR i32,                   //-IntKIND
chr OPERATOR u16,                   //-IntKIND
chr OPERATOR nchr,                  //-LiftedIntKIND
chr OPERATOR ni16,                  //-LiftedIntKIND
chr OPERATOR ni32,                  //-LiftedIntKIND
chr OPERATOR nu16,                  //-LiftedIntKIND

i16 OPERATOR chr,                   //-IntKIND
i16 OPERATOR i16,                   //-IntKIND
i16 OPERATOR i32,                   //-IntKIND
i16 OPERATOR u16,                   //-IntKIND
i16 OPERATOR nchr,                  //-LiftedIntKIND
i16 OPERATOR ni16,                  //-LiftedIntKIND
i16 OPERATOR ni32,                  //-LiftedIntKIND
i16 OPERATOR nu16,                  //-LiftedIntKIND

i32 OPERATOR chr,                   //-IntKIND
i32 OPERATOR i16,                   //-IntKIND
i32 OPERATOR i32,                   //-IntKIND
i32 OPERATOR u16,                   //-IntKIND
i32 OPERATOR nchr,                  //-LiftedIntKIND
i32 OPERATOR ni16,                  //-LiftedIntKIND
i32 OPERATOR ni32,                  //-LiftedIntKIND
i32 OPERATOR nu16,                  //-LiftedIntKIND

i64 OPERATOR chr,                   //-LongKIND
i64 OPERATOR i16,                   //-LongKIND
i64 OPERATOR i32,                   //-LongKIND
i64 OPERATOR u16,                   //-LongKIND
i64 OPERATOR nchr,                  //-LiftedLongKIND
i64 OPERATOR ni16,                  //-LiftedLongKIND
i64 OPERATOR ni32,                  //-LiftedLongKIND
i64 OPERATOR nu16,                  //-LiftedLongKIND

u16 OPERATOR chr,                   //-IntKIND
u16 OPERATOR i16,                   //-IntKIND
u16 OPERATOR i32,                   //-IntKIND
u16 OPERATOR u16,                   //-IntKIND
u16 OPERATOR nchr,                  //-LiftedIntKIND
u16 OPERATOR ni16,                  //-LiftedIntKIND
u16 OPERATOR ni32,                  //-LiftedIntKIND
u16 OPERATOR nu16,                  //-LiftedIntKIND

u32 OPERATOR chr,                   //-UIntKIND
u32 OPERATOR i16,                   //-UIntKIND
u32 OPERATOR i32,                   //-UIntKIND
u32 OPERATOR u16,                   //-UIntKIND
u32 OPERATOR nchr,                  //-LiftedUIntKIND
u32 OPERATOR ni16,                  //-LiftedUIntKIND
u32 OPERATOR ni32,                  //-LiftedUIntKIND
u32 OPERATOR nu16,                  //-LiftedUIntKIND

u64 OPERATOR chr,                   //-ULongKIND
u64 OPERATOR i16,                   //-ULongKIND
u64 OPERATOR i32,                   //-ULongKIND
u64 OPERATOR u16,                   //-ULongKIND
u64 OPERATOR nchr,                  //-LiftedULongKIND
u64 OPERATOR ni16,                  //-LiftedULongKIND
u64 OPERATOR ni32,                  //-LiftedULongKIND
u64 OPERATOR nu16,                  //-LiftedULongKIND

nchr OPERATOR chr,                   //-LiftedIntKIND
nchr OPERATOR i16,                   //-LiftedIntKIND
nchr OPERATOR i32,                   //-LiftedIntKIND
nchr OPERATOR u16,                   //-LiftedIntKIND
nchr OPERATOR nchr,                  //-LiftedIntKIND
nchr OPERATOR ni16,                  //-LiftedIntKIND
nchr OPERATOR ni32,                  //-LiftedIntKIND
nchr OPERATOR nu16,                  //-LiftedIntKIND

ni16 OPERATOR chr,                   //-LiftedIntKIND
ni16 OPERATOR i16,                   //-LiftedIntKIND
ni16 OPERATOR i32,                   //-LiftedIntKIND
ni16 OPERATOR u16,                   //-LiftedIntKIND
ni16 OPERATOR nchr,                  //-LiftedIntKIND
ni16 OPERATOR ni16,                  //-LiftedIntKIND
ni16 OPERATOR ni32,                  //-LiftedIntKIND
ni16 OPERATOR nu16,                  //-LiftedIntKIND

ni32 OPERATOR chr,                   //-LiftedIntKIND
ni32 OPERATOR i16,                   //-LiftedIntKIND
ni32 OPERATOR i32,                   //-LiftedIntKIND
ni32 OPERATOR u16,                   //-LiftedIntKIND
ni32 OPERATOR nchr,                  //-LiftedIntKIND
ni32 OPERATOR ni16,                  //-LiftedIntKIND
ni32 OPERATOR ni32,                  //-LiftedIntKIND
ni32 OPERATOR nu16,                  //-LiftedIntKIND

ni64 OPERATOR chr,                   //-LiftedLongKIND
ni64 OPERATOR i16,                   //-LiftedLongKIND
ni64 OPERATOR i32,                   //-LiftedLongKIND
ni64 OPERATOR u16,                   //-LiftedLongKIND
ni64 OPERATOR nchr,                  //-LiftedLongKIND
ni64 OPERATOR ni16,                  //-LiftedLongKIND
ni64 OPERATOR ni32,                  //-LiftedLongKIND
ni64 OPERATOR nu16,                  //-LiftedLongKIND

nu16 OPERATOR chr,                   //-LiftedIntKIND
nu16 OPERATOR i16,                   //-LiftedIntKIND
nu16 OPERATOR i32,                   //-LiftedIntKIND
nu16 OPERATOR u16,                   //-LiftedIntKIND
nu16 OPERATOR nchr,                  //-LiftedIntKIND
nu16 OPERATOR ni16,                  //-LiftedIntKIND
nu16 OPERATOR ni32,                  //-LiftedIntKIND
nu16 OPERATOR nu16,                  //-LiftedIntKIND

nu32 OPERATOR chr,                   //-LiftedUIntKIND
nu32 OPERATOR i16,                   //-LiftedUIntKIND
nu32 OPERATOR i32,                   //-LiftedUIntKIND
nu32 OPERATOR u16,                   //-LiftedUIntKIND
nu32 OPERATOR nchr,                  //-LiftedUIntKIND
nu32 OPERATOR ni16,                  //-LiftedUIntKIND
nu32 OPERATOR ni32,                  //-LiftedUIntKIND
nu32 OPERATOR nu16,                  //-LiftedUIntKIND

nu64 OPERATOR chr,                   //-LiftedULongKIND
nu64 OPERATOR i16,                   //-LiftedULongKIND
nu64 OPERATOR i32,                   //-LiftedULongKIND
nu64 OPERATOR u16,                   //-LiftedULongKIND
nu64 OPERATOR nchr,                  //-LiftedULongKIND
nu64 OPERATOR ni16,                  //-LiftedULongKIND
nu64 OPERATOR ni32,                  //-LiftedULongKIND
nu64 OPERATOR nu16                   //-LiftedULongKIND
" + Postfix;

        const string LogicTemplate = Prefix + @"
bln OPERATOR bln,                   //-BoolKIND
bln OPERATOR nbln,                  //-LiftedBoolKIND

nbln OPERATOR bln,                   //-LiftedBoolKIND
nbln OPERATOR nbln,                  //-LiftedBoolKIND

chr OPERATOR chr,                   //-IntKIND
chr OPERATOR i16,                   //-IntKIND
chr OPERATOR i32,                   //-IntKIND
chr OPERATOR i64,                   //-LongKIND
chr OPERATOR u16,                   //-IntKIND
chr OPERATOR u32,                   //-UIntKIND
chr OPERATOR u64,                   //-ULongKIND
chr OPERATOR nchr,                  //-LiftedIntKIND
chr OPERATOR ni16,                  //-LiftedIntKIND
chr OPERATOR ni32,                  //-LiftedIntKIND
chr OPERATOR ni64,                  //-LiftedLongKIND
chr OPERATOR nu16,                  //-LiftedIntKIND
chr OPERATOR nu32,                  //-LiftedUIntKIND
chr OPERATOR nu64,                  //-LiftedULongKIND

i16 OPERATOR chr,                   //-IntKIND
i16 OPERATOR i16,                   //-IntKIND
i16 OPERATOR i32,                   //-IntKIND
i16 OPERATOR i64,                   //-LongKIND
i16 OPERATOR u16,                   //-IntKIND
i16 OPERATOR u32,                   //-LongKIND
// i16 OPERATOR u64,
i16 OPERATOR nchr,                   //-LiftedIntKIND
i16 OPERATOR ni16,                  //-LiftedIntKIND
i16 OPERATOR ni32,                  //-LiftedIntKIND
i16 OPERATOR ni64,                  //-LiftedLongKIND
i16 OPERATOR nu16,                  //-LiftedIntKIND
i16 OPERATOR nu32,                  //-LiftedLongKIND
//i16 OPERATOR nu64,

i32 OPERATOR chr,                   //-IntKIND
i32 OPERATOR i16,                   //-IntKIND
i32 OPERATOR i32,                   //-IntKIND
i32 OPERATOR i64,                   //-LongKIND
i32 OPERATOR u16,                   //-IntKIND
i32 OPERATOR u32,                   //-LongKIND
//i32 OPERATOR u64,
i32 OPERATOR nchr,                  //-LiftedIntKIND
i32 OPERATOR ni16,                  //-LiftedIntKIND
i32 OPERATOR ni32,                  //-LiftedIntKIND
i32 OPERATOR ni64,                  //-LiftedLongKIND
i32 OPERATOR nu16,                  //-LiftedIntKIND
i32 OPERATOR nu32,                  //-LiftedLongKIND
//i32 OPERATOR nu64,

i64 OPERATOR chr,                   //-LongKIND
i64 OPERATOR i16,                   //-LongKIND
i64 OPERATOR i32,                   //-LongKIND
i64 OPERATOR i64,                   //-LongKIND
i64 OPERATOR u16,                   //-LongKIND
i64 OPERATOR u32,                   //-LongKIND
//i64 OPERATOR u64,
i64 OPERATOR nchr,                  //-LiftedLongKIND
i64 OPERATOR ni16,                  //-LiftedLongKIND
i64 OPERATOR ni32,                  //-LiftedLongKIND
i64 OPERATOR ni64,                  //-LiftedLongKIND
i64 OPERATOR nu16,                  //-LiftedLongKIND
i64 OPERATOR nu32,                  //-LiftedLongKIND
//i64 OPERATOR nu64,

u16 OPERATOR chr,                   //-IntKIND
u16 OPERATOR i16,                   //-IntKIND
u16 OPERATOR i32,                   //-IntKIND
u16 OPERATOR i64,                   //-LongKIND
u16 OPERATOR u16,                   //-IntKIND
u16 OPERATOR u32,                   //-UIntKIND
u16 OPERATOR u64,                   //-ULongKIND
u16 OPERATOR nchr,                  //-LiftedIntKIND
u16 OPERATOR ni16,                  //-LiftedIntKIND
u16 OPERATOR ni32,                  //-LiftedIntKIND
u16 OPERATOR ni64,                  //-LiftedLongKIND
u16 OPERATOR nu16,                  //-LiftedIntKIND
u16 OPERATOR nu32,                  //-LiftedUIntKIND
u16 OPERATOR nu64,                  //-LiftedULongKIND

u32 OPERATOR chr,                   //-UIntKIND
u32 OPERATOR i16,                   //-LongKIND
u32 OPERATOR i32,                   //-LongKIND
u32 OPERATOR i64,                   //-LongKIND
u32 OPERATOR u16,                   //-UIntKIND
u32 OPERATOR u32,                   //-UIntKIND
u32 OPERATOR u64,                   //-ULongKIND
u32 OPERATOR nchr,                   //-LiftedUIntKIND
u32 OPERATOR ni16,                  //-LiftedLongKIND
u32 OPERATOR ni32,                  //-LiftedLongKIND
u32 OPERATOR ni64,                  //-LiftedLongKIND
u32 OPERATOR nu16,                  //-LiftedUIntKIND
u32 OPERATOR nu32,                  //-LiftedUIntKIND
u32 OPERATOR nu64,                  //-LiftedULongKIND

u64 OPERATOR chr,                   //-ULongKIND
//u64 OPERATOR i16,
//u64 OPERATOR i32,
//u64 OPERATOR i64,
u64 OPERATOR u16,                   //-ULongKIND
u64 OPERATOR u32,                   //-ULongKIND
u64 OPERATOR u64,                   //-ULongKIND
u64 OPERATOR nchr,                   //-LiftedULongKIND
//u64 OPERATOR ni16,
//u64 OPERATOR ni32,
//u64 OPERATOR ni64,
u64 OPERATOR nu16,                  //-LiftedULongKIND
u64 OPERATOR nu32,                  //-LiftedULongKIND
u64 OPERATOR nu64,                  //-LiftedULongKIND

nchr OPERATOR chr,                   //-LiftedIntKIND
nchr OPERATOR i16,                   //-LiftedIntKIND
nchr OPERATOR i32,                   //-LiftedIntKIND
nchr OPERATOR i64,                   //-LiftedLongKIND
nchr OPERATOR u16,                   //-LiftedIntKIND
nchr OPERATOR u32,                   //-LiftedUIntKIND
nchr OPERATOR u64,                   //-LiftedULongKIND
nchr OPERATOR nchr,                  //-LiftedIntKIND
nchr OPERATOR ni16,                  //-LiftedIntKIND
nchr OPERATOR ni32,                  //-LiftedIntKIND
nchr OPERATOR ni64,                  //-LiftedLongKIND
nchr OPERATOR nu16,                  //-LiftedIntKIND
nchr OPERATOR nu32,                  //-LiftedUIntKIND
nchr OPERATOR nu64,                  //-LiftedULongKIND

ni16 OPERATOR chr,                   //-LiftedIntKIND
ni16 OPERATOR i16,                   //-LiftedIntKIND
ni16 OPERATOR i32,                   //-LiftedIntKIND
ni16 OPERATOR i64,                   //-LiftedLongKIND
ni16 OPERATOR u16,                   //-LiftedIntKIND
ni16 OPERATOR u32,                   //-LiftedLongKIND
//ni16 OPERATOR u64,
ni16 OPERATOR nchr,                   //-LiftedIntKIND
ni16 OPERATOR ni16,                  //-LiftedIntKIND
ni16 OPERATOR ni32,                  //-LiftedIntKIND
ni16 OPERATOR ni64,                  //-LiftedLongKIND
ni16 OPERATOR nu16,                  //-LiftedIntKIND
ni16 OPERATOR nu32,                  //-LiftedLongKIND
//ni16 OPERATOR nu64,

ni32 OPERATOR chr,                   //-LiftedIntKIND
ni32 OPERATOR i16,                   //-LiftedIntKIND
ni32 OPERATOR i32,                   //-LiftedIntKIND
ni32 OPERATOR i64,                   //-LiftedLongKIND
ni32 OPERATOR u16,                   //-LiftedIntKIND
ni32 OPERATOR u32,                   //-LiftedLongKIND
//ni32 OPERATOR u64,
ni32 OPERATOR nchr,                   //-LiftedIntKIND
ni32 OPERATOR ni16,                  //-LiftedIntKIND
ni32 OPERATOR ni32,                  //-LiftedIntKIND
ni32 OPERATOR ni64,                  //-LiftedLongKIND
ni32 OPERATOR nu16,                  //-LiftedIntKIND
ni32 OPERATOR nu32,                  //-LiftedLongKIND
//ni32 OPERATOR nu64,

ni64 OPERATOR chr,                   //-LiftedLongKIND
ni64 OPERATOR i16,                   //-LiftedLongKIND
ni64 OPERATOR i32,                   //-LiftedLongKIND
ni64 OPERATOR i64,                   //-LiftedLongKIND
ni64 OPERATOR u16,                   //-LiftedLongKIND
ni64 OPERATOR u32,                   //-LiftedLongKIND
//ni64 OPERATOR u64,
ni64 OPERATOR nchr,                  //-LiftedLongKIND
ni64 OPERATOR ni16,                  //-LiftedLongKIND
ni64 OPERATOR ni32,                  //-LiftedLongKIND
ni64 OPERATOR ni64,                  //-LiftedLongKIND
ni64 OPERATOR nu16,                  //-LiftedLongKIND
ni64 OPERATOR nu32,                  //-LiftedLongKIND
//ni64 OPERATOR nu64,

nu16 OPERATOR chr,                   //-LiftedIntKIND
nu16 OPERATOR i16,                   //-LiftedIntKIND
nu16 OPERATOR i32,                   //-LiftedIntKIND
nu16 OPERATOR i64,                   //-LiftedLongKIND
nu16 OPERATOR u16,                   //-LiftedIntKIND
nu16 OPERATOR u32,                   //-LiftedUIntKIND
nu16 OPERATOR u64,                   //-LiftedULongKIND
nu16 OPERATOR nchr,                  //-LiftedIntKIND
nu16 OPERATOR ni16,                  //-LiftedIntKIND
nu16 OPERATOR ni32,                  //-LiftedIntKIND
nu16 OPERATOR ni64,                  //-LiftedLongKIND
nu16 OPERATOR nu16,                  //-LiftedIntKIND
nu16 OPERATOR nu32,                  //-LiftedUIntKIND
nu16 OPERATOR nu64,                  //-LiftedULongKIND

nu32 OPERATOR chr,                   //-LiftedUIntKIND
nu32 OPERATOR i16,                   //-LiftedLongKIND
nu32 OPERATOR i32,                   //-LiftedLongKIND
nu32 OPERATOR i64,                   //-LiftedLongKIND
nu32 OPERATOR u16,                   //-LiftedUIntKIND
nu32 OPERATOR u32,                   //-LiftedUIntKIND
nu32 OPERATOR u64,                   //-LiftedULongKIND
nu32 OPERATOR nchr,                   //-LiftedUIntKIND
nu32 OPERATOR ni16,                  //-LiftedLongKIND
nu32 OPERATOR ni32,                  //-LiftedLongKIND
nu32 OPERATOR ni64,                  //-LiftedLongKIND
nu32 OPERATOR nu16,                  //-LiftedUIntKIND
nu32 OPERATOR nu32,                  //-LiftedUIntKIND
nu32 OPERATOR nu64,                  //-LiftedULongKIND

nu64 OPERATOR chr,                   //-LiftedULongKIND
//nu64 OPERATOR i16,
//nu64 OPERATOR i32,
//nu64 OPERATOR i64,
nu64 OPERATOR u16,                   //-LiftedULongKIND
nu64 OPERATOR u32,                   //-LiftedULongKIND
nu64 OPERATOR u64,                   //-LiftedULongKIND
nu64 OPERATOR nchr,                   //-LiftedULongKIND
//nu64 OPERATOR ni16,
//nu64 OPERATOR ni32,
//nu64 OPERATOR ni64,
nu64 OPERATOR nu16,                  //-LiftedULongKIND
nu64 OPERATOR nu32,                  //-LiftedULongKIND
nu64 OPERATOR nu64                  //-LiftedULongKIND
" + Postfix;

        //built-in operator only works for bools (not even lifted bools)
        const string ShortCircuitTemplate = Prefix + @"
bln OPERATOR bln,                   //-LogicalBoolKIND
" + Postfix;

        const string EnumLogicTemplate = Prefix + @"
e OPERATOR e,          //-EnumKIND
e OPERATOR ne,         //-LiftedEnumKIND
ne OPERATOR e,         //-LiftedEnumKIND
ne OPERATOR ne         //-LiftedEnumKIND" + Postfix;

        const string ComparisonTemplate = Prefix + @"
chr OPERATOR chr,                   //-IntKIND
chr OPERATOR i16,                   //-IntKIND
chr OPERATOR i32,                   //-IntKIND
chr OPERATOR i64,                   //-LongKIND
chr OPERATOR u16,                   //-IntKIND
chr OPERATOR u32,                   //-UIntKIND
chr OPERATOR u64,                   //-ULongKIND
chr OPERATOR r32,                   //-FloatKIND
chr OPERATOR r64,                   //-DoubleKIND
chr OPERATOR dec,                   //-DecimalKIND
chr OPERATOR nchr,                  //-LiftedIntKIND
chr OPERATOR ni16,                  //-LiftedIntKIND
chr OPERATOR ni32,                  //-LiftedIntKIND
chr OPERATOR ni64,                  //-LiftedLongKIND
chr OPERATOR nu16,                  //-LiftedIntKIND
chr OPERATOR nu32,                  //-LiftedUIntKIND
chr OPERATOR nu64,                  //-LiftedULongKIND
chr OPERATOR nr32,                  //-LiftedFloatKIND
chr OPERATOR nr64,                  //-LiftedDoubleKIND
chr OPERATOR ndec,                  //-LiftedDecimalKIND

i16 OPERATOR chr,                   //-IntKIND
i16 OPERATOR i16,                   //-IntKIND
i16 OPERATOR i32,                   //-IntKIND
i16 OPERATOR i64,                   //-LongKIND
i16 OPERATOR u16,                   //-IntKIND
i16 OPERATOR u32,                   //-LongKIND
// i16 OPERATOR u64, (ambiguous)
i16 OPERATOR r32,                   //-FloatKIND
i16 OPERATOR r64,                   //-DoubleKIND
i16 OPERATOR dec,                   //-DecimalKIND
i16 OPERATOR nchr,                   //-LiftedIntKIND
i16 OPERATOR ni16,                  //-LiftedIntKIND
i16 OPERATOR ni32,                  //-LiftedIntKIND
i16 OPERATOR ni64,                  //-LiftedLongKIND
i16 OPERATOR nu16,                  //-LiftedIntKIND
i16 OPERATOR nu32,                  //-LiftedLongKIND
// i16 OPERATOR nu64, (ambiguous)
i16 OPERATOR nr32,                  //-LiftedFloatKIND
i16 OPERATOR nr64,                  //-LiftedDoubleKIND
i16 OPERATOR ndec,                  //-LiftedDecimalKIND

i32 OPERATOR chr,                   //-IntKIND
i32 OPERATOR i16,                   //-IntKIND
i32 OPERATOR i32,                   //-IntKIND
i32 OPERATOR i64,                   //-LongKIND
i32 OPERATOR u16,                   //-IntKIND
i32 OPERATOR u32,                   //-LongKIND
// i32 OPERATOR u64, (ambiguous)
i32 OPERATOR r32,                   //-FloatKIND
i32 OPERATOR r64,                   //-DoubleKIND
i32 OPERATOR dec,                   //-DecimalKIND
i32 OPERATOR nchr,                  //-LiftedIntKIND
i32 OPERATOR ni16,                  //-LiftedIntKIND
i32 OPERATOR ni32,                  //-LiftedIntKIND
i32 OPERATOR ni64,                  //-LiftedLongKIND
i32 OPERATOR nu16,                  //-LiftedIntKIND
i32 OPERATOR nu32,                  //-LiftedLongKIND
// i32 OPERATOR nu64, (ambiguous)
i32 OPERATOR nr32,                  //-LiftedFloatKIND
i32 OPERATOR nr64,                  //-LiftedDoubleKIND
i32 OPERATOR ndec,                  //-LiftedDecimalKIND

i64 OPERATOR chr,                   //-LongKIND
i64 OPERATOR i16,                   //-LongKIND
i64 OPERATOR i32,                   //-LongKIND
i64 OPERATOR i64,                   //-LongKIND
i64 OPERATOR u16,                   //-LongKIND
i64 OPERATOR u32,                   //-LongKIND
// i64 OPERATOR u64, (ambiguous)
i64 OPERATOR r32,                   //-FloatKIND
i64 OPERATOR r64,                   //-DoubleKIND
i64 OPERATOR dec,                   //-DecimalKIND
i64 OPERATOR nchr,                  //-LiftedLongKIND
i64 OPERATOR ni16,                  //-LiftedLongKIND
i64 OPERATOR ni32,                  //-LiftedLongKIND
i64 OPERATOR ni64,                  //-LiftedLongKIND
i64 OPERATOR nu16,                  //-LiftedLongKIND
i64 OPERATOR nu32,                  //-LiftedLongKIND
// i64 OPERATOR nu64, (ambiguous)
i64 OPERATOR nr32,                  //-LiftedFloatKIND
i64 OPERATOR nr64,                  //-LiftedDoubleKIND
i64 OPERATOR ndec,                  //-LiftedDecimalKIND

u16 OPERATOR chr,                   //-IntKIND
u16 OPERATOR i16,                   //-IntKIND
u16 OPERATOR i32,                   //-IntKIND
u16 OPERATOR i64,                   //-LongKIND
u16 OPERATOR u16,                   //-IntKIND
u16 OPERATOR u32,                   //-UIntKIND
//u16 OPERATOR u64, (ambiguous)
u16 OPERATOR r32,                   //-FloatKIND
u16 OPERATOR r64,                   //-DoubleKIND
u16 OPERATOR dec,                   //-DecimalKIND
u16 OPERATOR nchr,                  //-LiftedIntKIND
u16 OPERATOR ni16,                  //-LiftedIntKIND
u16 OPERATOR ni32,                  //-LiftedIntKIND
u16 OPERATOR ni64,                  //-LiftedLongKIND
u16 OPERATOR nu16,                  //-LiftedIntKIND
u16 OPERATOR nu32,                  //-LiftedUIntKIND
//u16 OPERATOR nu64, (ambiguous)
u16 OPERATOR nr32,                  //-LiftedFloatKIND
u16 OPERATOR nr64,                  //-LiftedDoubleKIND
u16 OPERATOR ndec,                  //-LiftedDecimalKIND

u32 OPERATOR chr,                   //-UIntKIND
u32 OPERATOR i16,                   //-LongKIND
u32 OPERATOR i32,                   //-LongKIND
u32 OPERATOR i64,                   //-LongKIND
u32 OPERATOR u16,                   //-UIntKIND
u32 OPERATOR u32,                   //-UIntKIND
u32 OPERATOR u64,                   //-ULongKIND
u32 OPERATOR r32,                   //-FloatKIND
u32 OPERATOR r64,                   //-DoubleKIND
u32 OPERATOR dec,                   //-DecimalKIND
u32 OPERATOR nchr,                   //-LiftedUIntKIND
u32 OPERATOR ni16,                  //-LiftedLongKIND
u32 OPERATOR ni32,                  //-LiftedLongKIND
u32 OPERATOR ni64,                  //-LiftedLongKIND
u32 OPERATOR nu16,                  //-LiftedUIntKIND
u32 OPERATOR nu32,                  //-LiftedUIntKIND
u32 OPERATOR nu64,                  //-LiftedULongKIND
u32 OPERATOR nr32,                  //-LiftedFloatKIND
u32 OPERATOR nr64,                  //-LiftedDoubleKIND
u32 OPERATOR ndec,                  //-LiftedDecimalKIND

u64 OPERATOR chr,                   //-ULongKIND
// u64 OPERATOR i16, (ambiguous)
// u64 OPERATOR i32, (ambiguous)
// u64 OPERATOR i64, (ambiguous)
u64 OPERATOR u16,                   //-ULongKIND
u64 OPERATOR u32,                   //-ULongKIND
u64 OPERATOR u64,                   //-ULongKIND
u64 OPERATOR r32,                   //-FloatKIND
u64 OPERATOR r64,                   //-DoubleKIND
u64 OPERATOR dec,                   //-DecimalKIND
u64 OPERATOR nchr,                   //-LiftedULongKIND
// u64 OPERATOR ni16, (ambiguous)
// u64 OPERATOR ni32, (ambiguous)
// u64 OPERATOR ni64, (ambiguous)
u64 OPERATOR nu16,                  //-LiftedULongKIND
u64 OPERATOR nu32,                  //-LiftedULongKIND
u64 OPERATOR nu64,                  //-LiftedULongKIND
u64 OPERATOR nr32,                  //-LiftedFloatKIND
u64 OPERATOR nr64,                  //-LiftedDoubleKIND
u64 OPERATOR ndec,                  //-LiftedDecimalKIND

r32 OPERATOR chr,                   //-FloatKIND
r32 OPERATOR i16,                   //-FloatKIND
r32 OPERATOR i32,                   //-FloatKIND
r32 OPERATOR i64,                   //-FloatKIND
r32 OPERATOR u16,                   //-FloatKIND
r32 OPERATOR u32,                   //-FloatKIND
r32 OPERATOR u64,                   //-FloatKIND
r32 OPERATOR r32,                   //-FloatKIND
r32 OPERATOR r64,                   //-DoubleKIND
// r32 OPERATOR dec, (none applicable)
r32 OPERATOR nchr,                  //-LiftedFloatKIND
r32 OPERATOR ni16,                  //-LiftedFloatKIND
r32 OPERATOR ni32,                  //-LiftedFloatKIND
r32 OPERATOR ni64,                  //-LiftedFloatKIND
r32 OPERATOR nu16,                  //-LiftedFloatKIND
r32 OPERATOR nu32,                  //-LiftedFloatKIND
r32 OPERATOR nu64,                  //-LiftedFloatKIND
r32 OPERATOR nr32,                  //-LiftedFloatKIND
r32 OPERATOR nr64,                  //-LiftedDoubleKIND
// r32 OPERATOR ndec, (none applicable)

r64 OPERATOR chr,                   //-DoubleKIND
r64 OPERATOR i16,                   //-DoubleKIND
r64 OPERATOR i32,                   //-DoubleKIND
r64 OPERATOR i64,                   //-DoubleKIND
r64 OPERATOR u16,                   //-DoubleKIND
r64 OPERATOR u32,                   //-DoubleKIND
r64 OPERATOR u64,                   //-DoubleKIND
r64 OPERATOR r32,                   //-DoubleKIND
r64 OPERATOR r64,                   //-DoubleKIND
// r64 OPERATOR dec, (none applicable)
r64 OPERATOR nchr,                  //-LiftedDoubleKIND
r64 OPERATOR ni16,                  //-LiftedDoubleKIND
r64 OPERATOR ni32,                  //-LiftedDoubleKIND
r64 OPERATOR ni64,                  //-LiftedDoubleKIND
r64 OPERATOR nu16,                  //-LiftedDoubleKIND
r64 OPERATOR nu32,                  //-LiftedDoubleKIND
r64 OPERATOR nu64,                  //-LiftedDoubleKIND
r64 OPERATOR nr32,                  //-LiftedDoubleKIND
r64 OPERATOR nr64,                  //-LiftedDoubleKIND
// r64 OPERATOR ndec, (none applicable)

dec OPERATOR chr,                   //-DecimalKIND
dec OPERATOR i16,                   //-DecimalKIND
dec OPERATOR i32,                   //-DecimalKIND
dec OPERATOR i64,                   //-DecimalKIND
dec OPERATOR u16,                   //-DecimalKIND
dec OPERATOR u32,                   //-DecimalKIND
dec OPERATOR u64,                   //-DecimalKIND
// dec OPERATOR r32, (none applicable)
// dec OPERATOR r64, (none applicable)
dec OPERATOR dec,                   //-DecimalKIND
dec OPERATOR nchr,                  //-LiftedDecimalKIND
dec OPERATOR ni16,                  //-LiftedDecimalKIND
dec OPERATOR ni32,                  //-LiftedDecimalKIND
dec OPERATOR ni64,                  //-LiftedDecimalKIND
dec OPERATOR nu16,                  //-LiftedDecimalKIND
dec OPERATOR nu32,                  //-LiftedDecimalKIND
dec OPERATOR nu64,                  //-LiftedDecimalKIND
// dec OPERATOR nr32,   (none applicable)
// dec OPERATOR nr64,  (none applicable)
dec OPERATOR ndec,                   //-LiftedDecimalKIND

nchr OPERATOR chr,                   //-LiftedIntKIND
nchr OPERATOR i16,                   //-LiftedIntKIND
nchr OPERATOR i32,                   //-LiftedIntKIND
nchr OPERATOR i64,                   //-LiftedLongKIND
nchr OPERATOR u16,                   //-LiftedIntKIND
nchr OPERATOR u32,                   //-LiftedUIntKIND
nchr OPERATOR u64,                   //-LiftedULongKIND
nchr OPERATOR r32,                   //-LiftedFloatKIND
nchr OPERATOR r64,                   //-LiftedDoubleKIND
nchr OPERATOR dec,                   //-LiftedDecimalKIND
nchr OPERATOR nchr,                  //-LiftedIntKIND
nchr OPERATOR ni16,                  //-LiftedIntKIND
nchr OPERATOR ni32,                  //-LiftedIntKIND
nchr OPERATOR ni64,                  //-LiftedLongKIND
nchr OPERATOR nu16,                  //-LiftedIntKIND
nchr OPERATOR nu32,                  //-LiftedUIntKIND
nchr OPERATOR nu64,                  //-LiftedULongKIND
nchr OPERATOR nr32,                  //-LiftedFloatKIND
nchr OPERATOR nr64,                  //-LiftedDoubleKIND
nchr OPERATOR ndec,                  //-LiftedDecimalKIND

ni16 OPERATOR chr,                   //-LiftedIntKIND
ni16 OPERATOR i16,                   //-LiftedIntKIND
ni16 OPERATOR i32,                   //-LiftedIntKIND
ni16 OPERATOR i64,                   //-LiftedLongKIND
ni16 OPERATOR u16,                   //-LiftedIntKIND
ni16 OPERATOR u32,                   //-LiftedLongKIND
// ni16 OPERATOR u64, (ambiguous)
ni16 OPERATOR r32,                   //-LiftedFloatKIND
ni16 OPERATOR r64,                   //-LiftedDoubleKIND
ni16 OPERATOR dec,                   //-LiftedDecimalKIND
ni16 OPERATOR nchr,                   //-LiftedIntKIND
ni16 OPERATOR ni16,                  //-LiftedIntKIND
ni16 OPERATOR ni32,                  //-LiftedIntKIND
ni16 OPERATOR ni64,                  //-LiftedLongKIND
ni16 OPERATOR nu16,                  //-LiftedIntKIND
ni16 OPERATOR nu32,                  //-LiftedLongKIND
// ni16 OPERATOR nu64, (ambiguous)
ni16 OPERATOR nr32,                  //-LiftedFloatKIND
ni16 OPERATOR nr64,                  //-LiftedDoubleKIND
ni16 OPERATOR ndec,                  //-LiftedDecimalKIND

ni32 OPERATOR chr,                   //-LiftedIntKIND
ni32 OPERATOR i16,                   //-LiftedIntKIND
ni32 OPERATOR i32,                   //-LiftedIntKIND
ni32 OPERATOR i64,                   //-LiftedLongKIND
ni32 OPERATOR u16,                   //-LiftedIntKIND
ni32 OPERATOR u32,                   //-LiftedLongKIND
// ni32 OPERATOR u64, (ambiguous)
ni32 OPERATOR r32,                   //-LiftedFloatKIND
ni32 OPERATOR r64,                   //-LiftedDoubleKIND
ni32 OPERATOR dec,                   //-LiftedDecimalKIND
ni32 OPERATOR nchr,                   //-LiftedIntKIND
ni32 OPERATOR ni16,                  //-LiftedIntKIND
ni32 OPERATOR ni32,                  //-LiftedIntKIND
ni32 OPERATOR ni64,                  //-LiftedLongKIND
ni32 OPERATOR nu16,                  //-LiftedIntKIND
ni32 OPERATOR nu32,                  //-LiftedLongKIND
// ni32 OPERATOR nu64, (ambiguous)
ni32 OPERATOR nr32,                  //-LiftedFloatKIND
ni32 OPERATOR nr64,                  //-LiftedDoubleKIND
ni32 OPERATOR ndec,                  //-LiftedDecimalKIND

ni64 OPERATOR chr,                   //-LiftedLongKIND
ni64 OPERATOR i16,                   //-LiftedLongKIND
ni64 OPERATOR i32,                   //-LiftedLongKIND
ni64 OPERATOR i64,                   //-LiftedLongKIND
ni64 OPERATOR u16,                   //-LiftedLongKIND
ni64 OPERATOR u32,                   //-LiftedLongKIND
// ni64 OPERATOR u64, (ambiguous)
ni64 OPERATOR r32,                   //-LiftedFloatKIND
ni64 OPERATOR r64,                   //-LiftedDoubleKIND
ni64 OPERATOR dec,                   //-LiftedDecimalKIND
ni64 OPERATOR nchr,                   //-LiftedLongKIND
ni64 OPERATOR ni16,                  //-LiftedLongKIND
ni64 OPERATOR ni32,                  //-LiftedLongKIND
ni64 OPERATOR ni64,                  //-LiftedLongKIND
ni64 OPERATOR nu16,                  //-LiftedLongKIND
ni64 OPERATOR nu32,                  //-LiftedLongKIND
// ni64 OPERATOR nu64, (ambiguous)
ni64 OPERATOR nr32,                  //-LiftedFloatKIND
ni64 OPERATOR nr64,                  //-LiftedDoubleKIND
ni64 OPERATOR ndec,                  //-LiftedDecimalKIND

nu16 OPERATOR chr,                   //-LiftedIntKIND
nu16 OPERATOR i16,                   //-LiftedIntKIND
nu16 OPERATOR i32,                   //-LiftedIntKIND
nu16 OPERATOR i64,                   //-LiftedLongKIND
nu16 OPERATOR u16,                   //-LiftedIntKIND
nu16 OPERATOR u32,                   //-LiftedUIntKIND
//nu16 OPERATOR u64, (ambiguous)
nu16 OPERATOR r32,                   //-LiftedFloatKIND
nu16 OPERATOR r64,                   //-LiftedDoubleKIND
nu16 OPERATOR dec,                   //-LiftedDecimalKIND
nu16 OPERATOR nchr,                   //-LiftedIntKIND
nu16 OPERATOR ni16,                  //-LiftedIntKIND
nu16 OPERATOR ni32,                  //-LiftedIntKIND
nu16 OPERATOR ni64,                  //-LiftedLongKIND
nu16 OPERATOR nu16,                  //-LiftedIntKIND
nu16 OPERATOR nu32,                  //-LiftedUIntKIND
//nu16 OPERATOR nu64, (ambiguous)
nu16 OPERATOR nr32,                  //-LiftedFloatKIND
nu16 OPERATOR nr64,                  //-LiftedDoubleKIND
nu16 OPERATOR ndec,                  //-LiftedDecimalKIND

nu32 OPERATOR chr,                   //-LiftedUIntKIND
nu32 OPERATOR i16,                   //-LiftedLongKIND
nu32 OPERATOR i32,                   //-LiftedLongKIND
nu32 OPERATOR i64,                   //-LiftedLongKIND
nu32 OPERATOR u16,                   //-LiftedUIntKIND
nu32 OPERATOR u32,                   //-LiftedUIntKIND
nu32 OPERATOR u64,                   //-LiftedULongKIND
nu32 OPERATOR r32,                   //-LiftedFloatKIND
nu32 OPERATOR r64,                   //-LiftedDoubleKIND
nu32 OPERATOR dec,                   //-LiftedDecimalKIND
nu32 OPERATOR nchr,                   //-LiftedUIntKIND
nu32 OPERATOR ni16,                  //-LiftedLongKIND
nu32 OPERATOR ni32,                  //-LiftedLongKIND
nu32 OPERATOR ni64,                  //-LiftedLongKIND
nu32 OPERATOR nu16,                  //-LiftedUIntKIND
nu32 OPERATOR nu32,                  //-LiftedUIntKIND
nu32 OPERATOR nu64,                  //-LiftedULongKIND
nu32 OPERATOR nr32,                  //-LiftedFloatKIND
nu32 OPERATOR nr64,                  //-LiftedDoubleKIND
nu32 OPERATOR ndec,                  //-LiftedDecimalKIND

nu64 OPERATOR chr,                   //-LiftedULongKIND
// nu64 OPERATOR i16, (ambiguous)
// nu64 OPERATOR i32, (ambiguous)
// nu64 OPERATOR i64, (ambiguous)
nu64 OPERATOR u16,                   //-LiftedULongKIND
nu64 OPERATOR u32,                   //-LiftedULongKIND
nu64 OPERATOR u64,                   //-LiftedULongKIND
nu64 OPERATOR r32,                   //-LiftedFloatKIND
nu64 OPERATOR r64,                   //-LiftedDoubleKIND
nu64 OPERATOR dec,                   //-LiftedDecimalKIND
nu64 OPERATOR nchr,                   //-LiftedULongKIND
// nu64 OPERATOR ni16, (ambiguous)
// nu64 OPERATOR ni32, (ambiguous)
// nu64 OPERATOR ni64, (ambiguous)
nu64 OPERATOR nu16,                  //-LiftedULongKIND
nu64 OPERATOR nu32,                  //-LiftedULongKIND
nu64 OPERATOR nu64,                  //-LiftedULongKIND
nu64 OPERATOR nr32,                  //-LiftedFloatKIND
nu64 OPERATOR nr64,                  //-LiftedDoubleKIND
nu64 OPERATOR ndec,                  //-LiftedDecimalKIND

nr32 OPERATOR chr,                   //-LiftedFloatKIND
nr32 OPERATOR i16,                   //-LiftedFloatKIND
nr32 OPERATOR i32,                   //-LiftedFloatKIND
nr32 OPERATOR i64,                   //-LiftedFloatKIND
nr32 OPERATOR u16,                   //-LiftedFloatKIND
nr32 OPERATOR u32,                   //-LiftedFloatKIND
nr32 OPERATOR u64,                   //-LiftedFloatKIND
nr32 OPERATOR r32,                   //-LiftedFloatKIND
nr32 OPERATOR r64,                   //-LiftedDoubleKIND
// nr32 OPERATOR dec, (none applicable)
nr32 OPERATOR nchr,                   //-LiftedFloatKIND
nr32 OPERATOR ni16,                  //-LiftedFloatKIND
nr32 OPERATOR ni32,                  //-LiftedFloatKIND
nr32 OPERATOR ni64,                  //-LiftedFloatKIND
nr32 OPERATOR nu16,                  //-LiftedFloatKIND
nr32 OPERATOR nu32,                  //-LiftedFloatKIND
nr32 OPERATOR nu64,                  //-LiftedFloatKIND
nr32 OPERATOR nr32,                  //-LiftedFloatKIND
nr32 OPERATOR nr64,                  //-LiftedDoubleKIND
// nr32 OPERATOR ndec, (none applicable)

nr64 OPERATOR chr,                   //-LiftedDoubleKIND
nr64 OPERATOR i16,                   //-LiftedDoubleKIND
nr64 OPERATOR i32,                   //-LiftedDoubleKIND
nr64 OPERATOR i64,                   //-LiftedDoubleKIND
nr64 OPERATOR u16,                   //-LiftedDoubleKIND
nr64 OPERATOR u32,                   //-LiftedDoubleKIND
nr64 OPERATOR u64,                   //-LiftedDoubleKIND
nr64 OPERATOR r32,                   //-LiftedDoubleKIND
nr64 OPERATOR r64,                   //-LiftedDoubleKIND
// nr64 OPERATOR dec, (none applicable)
nr64 OPERATOR nchr,                   //-LiftedDoubleKIND
nr64 OPERATOR ni16,                  //-LiftedDoubleKIND
nr64 OPERATOR ni32,                  //-LiftedDoubleKIND
nr64 OPERATOR ni64,                  //-LiftedDoubleKIND
nr64 OPERATOR nu16,                  //-LiftedDoubleKIND
nr64 OPERATOR nu32,                  //-LiftedDoubleKIND
nr64 OPERATOR nu64,                  //-LiftedDoubleKIND
nr64 OPERATOR nr32,                  //-LiftedDoubleKIND
nr64 OPERATOR nr64,                  //-LiftedDoubleKIND
// nr64 OPERATOR ndec, (none applicable)

ndec OPERATOR chr,                   //-LiftedDecimalKIND
ndec OPERATOR i16,                   //-LiftedDecimalKIND
ndec OPERATOR i32,                   //-LiftedDecimalKIND
ndec OPERATOR i64,                   //-LiftedDecimalKIND
ndec OPERATOR u16,                   //-LiftedDecimalKIND
ndec OPERATOR u32,                   //-LiftedDecimalKIND
ndec OPERATOR u64,                   //-LiftedDecimalKIND
// ndec OPERATOR r32, (none applicable)
// ndec OPERATOR r64, (none applicable)
ndec OPERATOR dec,                   //-LiftedDecimalKIND
ndec OPERATOR nchr,                   //-LiftedDecimalKIND
ndec OPERATOR ni16,                  //-LiftedDecimalKIND
ndec OPERATOR ni32,                  //-LiftedDecimalKIND
ndec OPERATOR ni64,                  //-LiftedDecimalKIND
ndec OPERATOR nu16,                  //-LiftedDecimalKIND
ndec OPERATOR nu32,                  //-LiftedDecimalKIND
ndec OPERATOR nu64,                  //-LiftedDecimalKIND
// ndec OPERATOR nr32,   (none applicable)
// ndec OPERATOR nr64,  (none applicable)
ndec OPERATOR ndec                    //-LiftedDecimalKIND
" + Postfix;

        const string EqualityTemplate = Prefix + @"
e1 OPERATOR e2, //-EnumKIND
e1 OPERATOR o2, //-KIND
d1 OPERATOR d2, //-DelegateKIND
d1 OPERATOR o2, //-ObjectKIND
s1 OPERATOR s2, //-StringKIND
s1 OPERATOR o2, //-ObjectKIND
o1 OPERATOR e2, //-KIND
o1 OPERATOR d2, //-ObjectKIND
o1 OPERATOR s2, //-ObjectKIND
o1 OPERATOR o2  //-ObjectKIND" + Postfix;

        const string PostfixIncrementTemplate = Prefix + @"
e   OPERATOR, //-EnumKIND
chr OPERATOR, //-CharKIND
i08 OPERATOR, //-SByteKIND
i16 OPERATOR, //-ShortKIND
i32 OPERATOR, //-IntKIND
i64 OPERATOR, //-LongKIND
u08 OPERATOR, //-ByteKIND
u16 OPERATOR, //-UShortKIND
u32 OPERATOR, //-UIntKIND
u64 OPERATOR, //-ULongKIND
r32 OPERATOR, //-FloatKIND
r64 OPERATOR, //-DoubleKIND
dec OPERATOR, //-DecimalKIND
ne  OPERATOR, //-LiftedEnumKIND
nchr OPERATOR, //-LiftedCharKIND
ni08 OPERATOR, //-LiftedSByteKIND
ni16 OPERATOR, //-LiftedShortKIND
ni32 OPERATOR, //-LiftedIntKIND
ni64 OPERATOR, //-LiftedLongKIND
nu08 OPERATOR, //-LiftedByteKIND
nu16 OPERATOR, //-LiftedUShortKIND
nu32 OPERATOR, //-LiftedUIntKIND
nu64 OPERATOR, //-LiftedULongKIND
nr32 OPERATOR, //-LiftedFloatKIND
nr64 OPERATOR, //-LiftedDoubleKIND
ndec OPERATOR  //-LiftedDecimalKIND
" + Postfix;

        const string PrefixIncrementTemplate = Prefix + @"
OPERATOR e   , //-EnumKIND
OPERATOR chr , //-CharKIND
OPERATOR i08 , //-SByteKIND
OPERATOR i16 , //-ShortKIND
OPERATOR i32 , //-IntKIND
OPERATOR i64 , //-LongKIND
OPERATOR u08 , //-ByteKIND
OPERATOR u16 , //-UShortKIND
OPERATOR u32 , //-UIntKIND
OPERATOR u64 , //-ULongKIND
OPERATOR r32 , //-FloatKIND
OPERATOR r64 , //-DoubleKIND
OPERATOR dec , //-DecimalKIND
OPERATOR ne  , //-LiftedEnumKIND
OPERATOR nchr , //-LiftedCharKIND
OPERATOR ni08 , //-LiftedSByteKIND
OPERATOR ni16 , //-LiftedShortKIND
OPERATOR ni32 , //-LiftedIntKIND
OPERATOR ni64 , //-LiftedLongKIND
OPERATOR nu08 , //-LiftedByteKIND
OPERATOR nu16 , //-LiftedUShortKIND
OPERATOR nu32 , //-LiftedUIntKIND
OPERATOR nu64 , //-LiftedULongKIND
OPERATOR nr32 , //-LiftedFloatKIND
OPERATOR nr64 , //-LiftedDoubleKIND
OPERATOR ndec   //-LiftedDecimalKIND" + Postfix;

        const string UnaryPlus = Prefix + @"
+ chr, //-IntUnaryPlus
+ i08, //-IntUnaryPlus
+ i16, //-IntUnaryPlus
+ i32, //-IntUnaryPlus
+ i64, //-LongUnaryPlus
+ u08, //-IntUnaryPlus
+ u16, //-IntUnaryPlus
+ u32, //-UIntUnaryPlus
+ u64, //-ULongUnaryPlus
+ r32, //-FloatUnaryPlus
+ r64, //-DoubleUnaryPlus
+ dec, //-DecimalUnaryPlus
+ nchr, //-LiftedIntUnaryPlus
+ ni08, //-LiftedIntUnaryPlus
+ ni16, //-LiftedIntUnaryPlus
+ ni32, //-LiftedIntUnaryPlus
+ ni64, //-LiftedLongUnaryPlus
+ nu08, //-LiftedIntUnaryPlus
+ nu16, //-LiftedIntUnaryPlus
+ nu32, //-LiftedUIntUnaryPlus
+ nu64, //-LiftedULongUnaryPlus
+ nr32, //-LiftedFloatUnaryPlus
+ nr64, //-LiftedDoubleUnaryPlus
+ ndec  //-LiftedDecimalUnaryPlus" + Postfix;


        const string UnaryMinus = Prefix + @"
- chr, //-IntUnaryMinus
- i08, //-IntUnaryMinus
- i16, //-IntUnaryMinus
- i32, //-IntUnaryMinus
- i64, //-LongUnaryMinus
- u08, //-IntUnaryMinus
- u16, //-IntUnaryMinus
- u32, //-LongUnaryMinus
- r32, //-FloatUnaryMinus
- r64, //-DoubleUnaryMinus
- dec, //-DecimalUnaryMinus
- nchr, //-LiftedIntUnaryMinus
- ni08, //-LiftedIntUnaryMinus
- ni16, //-LiftedIntUnaryMinus
- ni32, //-LiftedIntUnaryMinus
- ni64, //-LiftedLongUnaryMinus
- nu08, //-LiftedIntUnaryMinus
- nu16, //-LiftedIntUnaryMinus
- nu32, //-LiftedLongUnaryMinus
- nr32, //-LiftedFloatUnaryMinus
- nr64, //-LiftedDoubleUnaryMinus
- ndec  //-LiftedDecimalUnaryMinus" + Postfix;

        const string LogicalNegation = Prefix + @"
! bln, //-BoolLogicalNegation
! nbln //-LiftedBoolLogicalNegation" + Postfix;


        const string BitwiseComplement = Prefix + @"
~ e,   //-EnumBitwiseComplement
~ chr, //-IntBitwiseComplement
~ i08, //-IntBitwiseComplement
~ i16, //-IntBitwiseComplement
~ i32, //-IntBitwiseComplement
~ i64, //-LongBitwiseComplement
~ u08, //-IntBitwiseComplement
~ u16, //-IntBitwiseComplement
~ u32, //-UIntBitwiseComplement
~ u64, //-ULongBitwiseComplement
~ ne,   //-LiftedEnumBitwiseComplement
~ nchr, //-LiftedIntBitwiseComplement
~ ni08, //-LiftedIntBitwiseComplement
~ ni16, //-LiftedIntBitwiseComplement
~ ni32, //-LiftedIntBitwiseComplement
~ ni64, //-LiftedLongBitwiseComplement
~ nu08, //-LiftedIntBitwiseComplement
~ nu16, //-LiftedIntBitwiseComplement
~ nu32, //-LiftedUIntBitwiseComplement
~ nu64 //-LiftedULongBitwiseComplement
);
    }
}" + Postfix;

        #endregion

        [Fact, WorkItem(527598, "DevDiv")]
        public void UserDefinedOperatorOnPointerType()
        {
            CreateCompilationWithMscorlib(@"
unsafe struct A
{
    public static implicit operator int*(A x) { return null; }
    
    static void M()
    {
        var x = new A();
        int* y = null;
        var z = x - y; // Dev11 generates CS0019...should compile
    }
}
", options: TestOptions.UnsafeReleaseDll).VerifyDiagnostics();
            // add better verification once this is implemented
        }

        [Fact]
        public void TestNullCoalesce_Dynamic()
        {
            var source = @"
// a ?? b

public class E : D { } 
public class D { }
public class C
{
    public static int Main()
    {
        Dynamic_b_constant_null_a();
        Dynamic_b_constant_null_a_nullable();
        Dynamic_b_constant_not_null_a_nullable();
        Dynamic_b_not_null_a();
        Dynamic_b_not_null_a_nullable(10);
	    return 0;
    }

    public static D Dynamic_b_constant_null_a()
    {
        dynamic b = new D();
        D a = null;
        dynamic z = a ?? b;
        return z;
    }
    public static D Dynamic_b_constant_null_a_nullable()
    {
        dynamic b = new D();
        int? a = null;
        dynamic z = a ?? b;
        return z;
    }
    public static D Dynamic_b_constant_not_null_a_nullable()
    {
        dynamic b = new D();
        int? a = 10;
        dynamic z = a ?? b;
        return z;
    }
    public static D Dynamic_b_not_null_a()
    {
        dynamic b = new D();
        D a = new E();
        dynamic z = a ?? b;
        return z;
    }
    public static D Dynamic_b_not_null_a_nullable(int? c)
    {
        dynamic b = new D();
        int? a = c;
        dynamic z = a ?? b;
        return z;
    }
}
";
            var compilation = CreateCompilationWithMscorlib(source).VerifyDiagnostics();
        }

        [WorkItem(541147, "DevDiv")]
        [Fact]
        public void TestNullCoalesceWithMethodGroup()
        {
            var source = @"
using System;

static class Program
{
    static void Main()
    {
        Action a = Main ?? Main;
    }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "Main ?? Main").WithArguments("??", "method group", "method group"));
        }

        [WorkItem(541149, "DevDiv")]
        [Fact]
        public void TestNullCoalesceWithLambda()
        {
            var source = @"
using System;

static class Program
{
    static void Main()
    {
        const Action<int> a = null;
        var b = a ?? (() => { });
    }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "a ?? (() => { })").WithArguments("??", "System.Action<int>", "lambda expression"));
        }

        [WorkItem(541148, "DevDiv")]
        [Fact]
        public void TestNullCoalesceWithConstNonNullExpression()
        {
            var source = @"
using System;

static class Program
{
    static void Main()
    {
        const string x = ""A"";
        string y;
        string z = x ?? y;
        Console.WriteLine(z);
    }
}
";
            CompileAndVerify(source, expectedOutput: "A");
        }

        [WorkItem(545631, "DevDiv")]
        [Fact]
        public void TestNullCoalesceWithInvalidUserDefinedConversions_01()
        {
            var source = @"
class B
{
    static void Main()
    {
        A a = null;
        B b = null;
        var c = a ?? b;
    }
    public static implicit operator A(B x)
    {
        return new A();
    }
}
 
class A
{
    public static implicit operator A(B x)
    {
        return new A();
    }
    public static implicit operator B(A x)
    {
        return new B();
    }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (8,22): error CS0457: Ambiguous user defined conversions 'B.implicit operator A(B)' and 'A.implicit operator A(B)' when converting from 'B' to 'A'
                //         var c = a ?? b;
                Diagnostic(ErrorCode.ERR_AmbigUDConv, "b").WithArguments("B.implicit operator A(B)", "A.implicit operator A(B)", "B", "A"));
        }

        [WorkItem(545631, "DevDiv")]
        [Fact]
        public void TestNullCoalesceWithInvalidUserDefinedConversions_02()
        {
            var source = @"
struct B
{
    static void Main()
    {
        A? a = null;
        B b;
        var c = a ?? b;
    }
    public static implicit operator A(B x)
    {
        return new A();
    }
}
 
struct A
{
    public static implicit operator A(B x)
    {
        return new A();
    }
    public static implicit operator B(A x)
    {
        return new B();
    }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (8,22): error CS0457: Ambiguous user defined conversions 'B.implicit operator A(B)' and 'A.implicit operator A(B)' when converting from 'B' to 'A'
                //         var c = a ?? b;
                Diagnostic(ErrorCode.ERR_AmbigUDConv, "b").WithArguments("B.implicit operator A(B)", "A.implicit operator A(B)", "B", "A"));
        }

        [WorkItem(545631, "DevDiv")]
        [Fact]
        public void TestNullCoalesceWithInvalidUserDefinedConversions_03()
        {
            var source = @"
struct B
{
    static void Main()
    {
        A a2;
        B? b2 = null;
        var c2 = b2 ?? a2;
    }

    public static implicit operator A(B x)
    {
        return new A();
    }
}
 
struct A
{
    public static implicit operator A(B x)
    {
        return new A();
    }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (8,18): error CS0457: Ambiguous user defined conversions 'B.implicit operator A(B)' and 'A.implicit operator A(B)' when converting from 'B' to 'A'
                //         var c2 = b2 ?? a2;
                Diagnostic(ErrorCode.ERR_AmbigUDConv, "b2").WithArguments("B.implicit operator A(B)", "A.implicit operator A(B)", "B", "A"));
        }

        [WorkItem(541343, "DevDiv")]
        [Fact]
        public void TestAsOperator_Bug8014()
        {
            var source = @"
using System;
 
class Program
{
    static void Main()
    {
        object y = null as object ?? null;
    }
}
";
            CompileAndVerify(source, expectedOutput: string.Empty);
        }

        [WorkItem(542090, "DevDiv")]
        [Fact]
        public void TestAsOperatorWithImplicitConversion()
        {
            var source = @"
using System;
 
class Program
{
    static void Main()
    {
        object o = 5 as object;
        string s = ""str"" as string;
        s = null as string;
    }
}
";
            CompileAndVerify(source, expectedOutput: "");
        }

        [Fact]
        public void TestDefaultOperator_ConstantDateTime()
        {
            var source = @"
using System;
namespace N2
{
    class X
    {
        public static void Main()
        {
        }
        public static DateTime Foo()
        {
            return default(DateTime);
        }
    }
}";
            var comp = CompileAndVerify(source);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void TestDefaultOperator_Dynamic()
        {
            // "default(dynamic)" has constant value null.
            var source = @"
using System;

public class X
{
    public static void Main()
    {
        const object obj = default(dynamic);
        Console.Write(obj == null);
    }
}";
            var comp = CreateCompilationWithMscorlibAndSystemCore(source, options: TestOptions.ReleaseExe);
            CompileAndVerify(comp, expectedOutput: "True");;

            source = @"
using System;

public class C<T> { }
public class X
{
    public X(dynamic param = default(dynamic)) { Console.WriteLine(param == null); }

    public static void Main()
    {
        Console.Write(default(dynamic));
        Console.Write(default(C<dynamic>));

        Console.WriteLine(default(dynamic) == null);
        Console.WriteLine(default(C<dynamic>) == null);

        object x = default(dynamic);
        Console.WriteLine(x == null);

        var unused = new X();
    }
}";
            comp = CreateCompilationWithMscorlibAndSystemCore(source);
            comp.VerifyDiagnostics();


            // "default(dynamic)" has type dynamic
            source = @"
public class X
{
    public X(object param = default(dynamic)) {}
}";
            comp = CreateCompilationWithMscorlibAndSystemCore(source);
            comp.VerifyDiagnostics(
                // (4,21): error CS1750: A value of type 'dynamic' cannot be used as a default parameter because there are no standard conversions to type 'object'
                //     public X(object param = default(dynamic)) {}
                Diagnostic(ErrorCode.ERR_NoConversionForDefaultParam, "param").WithArguments("dynamic", "object")); 
        }

        [WorkItem(537876, "DevDiv")]
        [Fact]
        public void TestEnumOrAssign()
        {
            var source = @"
enum F
{
   A,
   B,
   C
}
class Program
{
    static void Main(string[] args)
    {
        F x = F.A;
        x |= F.B;
    }
}
";
            var comp = CompileAndVerify(source);
            comp.VerifyDiagnostics();
        }

        [WorkItem(542072, "DevDiv")]
        [Fact]
        public void TestEnumLogicalWithLiteralZero_9042()
        {
            var source = @"
enum F { A }
class Program
{
    static void Main()
    {
        M(F.A | 0);
        M(0 | F.A);
        M(F.A & 0);
        M(0 & F.A);
        M(F.A ^ 0);
        M(0 ^ F.A);
    }
    static void M(F f) {}
}
";
            var comp = CompileAndVerify(source);
            comp.VerifyDiagnostics();
        }

        [WorkItem(542073, "DevDiv")]
        [Fact]
        public void TestEnumCompoundAddition_9043()
        {
            var source = @"
enum F { A, B }
class Program
{
    static void Main()
    {
        F f = F.A;
        f += 1;
    }
} 
";
            var comp = CompileAndVerify(source);
            comp.VerifyDiagnostics();
        }

        [WorkItem(542086, "DevDiv")]
        [Fact]
        public void TestStringCompoundAddition_9146()
        {
            var source = @"
class Test
{
    public static void Main()
    {
        int i = 0;
        string s = ""i="";
        s += i;
    }
}
";
            var comp = CompileAndVerify(source);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void TestOpTrueInBooleanExpression()
        {
            var source = @"
class Program
{
    struct C
    {
        public int x;
        public static bool operator true(C c) { return c.x != 0; }
        public static bool operator false(C c) { return c.x == 0; } 
        public static bool operator true(C? c) { return c.HasValue && c.Value.x != 0; }
        public static bool operator false(C? c) { return c.HasValue && c.Value.x == 0; } 
    }

    static void Main()
    {
        C c = new C();
        c.x = 1;
        if (c) 
        {
            System.Console.WriteLine(1);
        }

        while(c)
        { 
            System.Console.WriteLine(2);
            c.x--;
        }

        for(c.x = 1; c; c.x--)
            System.Console.WriteLine(3);

        c.x = 1;
        do
        {
            System.Console.WriteLine(4);
            c.x--;
        }
        while(c);

        System.Console.WriteLine(c ? 6 : 5);
    
        C? c2 = c;

        System.Console.WriteLine(c2 ? 7 : 8);
    }
}";
            var comp = CreateCompilationWithMscorlib(source);
            comp.VerifyDiagnostics();

        }

        [Fact]
        public void TestOpTrueInBooleanExpressionError()
        {
            // operator true does not lift to nullable.
            var source = @"
class Program
{
    struct C
    {
        public int x;
        public static bool operator true(C c) { return c.x != 0; }
        public static bool operator false(C c) { return c.x == 0; } 
    }

    static void Main()
    {
        C? c = new C();
        if (c) 
        {
            System.Console.WriteLine(1);
        }
    }
}";
            var comp = CreateCompilationWithMscorlib(source);
            comp.VerifyDiagnostics(
                // (14,13): error CS0029: Cannot implicitly convert type 'Program.C?' to 'bool'
                //         if (c) 
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "c").WithArguments("Program.C?", "bool"),
                // (6,20): warning CS0649: Field 'Program.C.x' is never assigned to, and will always have its default value 0
                //         public int x;
                Diagnostic(ErrorCode.WRN_UnassignedInternalField, "x").WithArguments("Program.C.x", "0")
                );
        }

        [WorkItem(543294, "DevDiv")]
        [Fact()]
        public void TestAsOperatorWithTypeParameter()
        {
            // SPEC:    Furthermore, at least one of the following must be true, or otherwise a compile-time error occurs:
            // SPEC:    - An identity (�6.1.1), implicit nullable (�6.1.4), implicit reference (�6.1.6), boxing (�6.1.7), 
            // SPEC:        explicit nullable (�6.2.3), explicit reference (�6.2.4), or unboxing (�6.2.5) conversion exists
            // SPEC:        from E to T.
            // SPEC:    - The type of E or T is an open type.
            // SPEC:    - E is the null literal.

            // SPEC VIOLATION:  The specification unintentionally allows the case where requirement 2 above:
            // SPEC VIOLATION:  "The type of E or T is an open type" is true, but type of E is void type, i.e. T is an open type.
            // SPEC VIOLATION:  Dev10 compiler correctly generates an error for this case and we will maintain compatibility.

            var source = @"
using System;

class Program
{
    static void Main()
    {
        Foo<Action>();
    }

    static void Foo<T>() where T : class
    {
        object o = Main() as T;
    }
}
";
            var comp = CreateCompilationWithMscorlib(source);
            comp.VerifyDiagnostics(
                // (13,20): error CS0039: Cannot convert type 'void' to 'T' via a reference conversion, boxing conversion, unboxing conversion, wrapping conversion, or null type conversion
                //         object o = Main() as T;
                Diagnostic(ErrorCode.ERR_NoExplicitBuiltinConv, "Main() as T").WithArguments("void", "T"));
        }

        [WorkItem(543294, "DevDiv")]
        [Fact()]
        public void TestIsOperatorWithTypeParameter()
        {
            var source = @"
using System;

class Program
{
    static void Main()
    {
        Foo<Action>();
    }

    static void Foo<T>() where T : class
    {
        bool b = Main() is T;
    }
}
";
            // NOTE:    Dev10 violates the SPEC for this test case and generates
            // NOTE:    an error ERR_NoExplicitBuiltinConv if the target type
            // NOTE:    is an open type. According to the specification, the result
            // NOTE:    is always false, but no compile time error occurs.
            // NOTE:    We follow the specification and generate WRN_IsAlwaysFalse
            // NOTE:    instead of an error.
            
            var comp = CreateCompilationWithMscorlib(source);
            comp.VerifyDiagnostics(
                // (13,18): warning CS0184: The given expression is never of the provided ('T') type
                //         bool b = Main() is T;
                Diagnostic(ErrorCode.WRN_IsAlwaysFalse, "Main() is T").WithArguments("T"));
        }

        [WorkItem(844635, "DevDiv")]
        [Fact()]
        public void TestIsOperatorWithGenericContainingType()
        {
            var source = @"
class Program
{
    static void Foo<T>(
        Outer<T>.C c1, Outer<int>.C c2,
        Outer<T>.S s1, Outer<int>.S s2,
        Outer<T>.E e1, Outer<int>.E e2)
    {
        bool b;
        b = c1 is Outer<T>.C;       // Deferred to runtime - null check.
        b = c1 is Outer<int>.C;     // Deferred to runtime - null check.
        b = c1 is Outer<long>.C;    // Deferred to runtime - null check.

        b = c2 is Outer<T>.C;       // Deferred to runtime - null check.
        b = c2 is Outer<int>.C;     // Deferred to runtime - null check.
        b = c2 is Outer<long>.C;    // Always false.

        b = s1 is Outer<T>.S;       // Always true.
        b = s1 is Outer<int>.S;     // Deferred to runtime - type unification.
        b = s1 is Outer<long>.S;    // Deferred to runtime - type unification.

        b = s2 is Outer<T>.S;       // Deferred to runtime - type unification.
        b = s2 is Outer<int>.S;     // Always true.
        b = s2 is Outer<long>.S;    // Always false.

        b = e1 is Outer<T>.E;       // Always true.
        b = e1 is Outer<int>.E;     // Deferred to runtime - type unification.
        b = e1 is Outer<long>.E;    // Deferred to runtime - type unification.

        b = e2 is Outer<T>.E;       // Deferred to runtime - type unification.
        b = e2 is Outer<int>.E;     // Always true.
        b = e2 is Outer<long>.E;    // Always false.
    }
}

class Outer<T>
{
    public class C { }
    public struct S { }
    public enum E { }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (16,13): warning CS0184: The given expression is never of the provided ('Outer<long>.C') type
                //         b = c2 is Outer<long>.C;    // Always false.
                Diagnostic(ErrorCode.WRN_IsAlwaysFalse, "c2 is Outer<long>.C").WithArguments("Outer<long>.C").WithLocation(16, 13),
                // (18,13): warning CS0183: The given expression is always of the provided ('Outer<T>.S') type
                //         b = s1 is Outer<T>.S;       // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "s1 is Outer<T>.S").WithArguments("Outer<T>.S").WithLocation(18, 13),
                // (23,13): warning CS0183: The given expression is always of the provided ('Outer<int>.S') type
                //         b = s2 is Outer<int>.S;     // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "s2 is Outer<int>.S").WithArguments("Outer<int>.S").WithLocation(23, 13),
                // (24,13): warning CS0184: The given expression is never of the provided ('Outer<long>.S') type
                //         b = s2 is Outer<long>.S;    // Always false.
                Diagnostic(ErrorCode.WRN_IsAlwaysFalse, "s2 is Outer<long>.S").WithArguments("Outer<long>.S").WithLocation(24, 13),
                // (26,13): warning CS0183: The given expression is always of the provided ('Outer<T>.E') type
                //         b = e1 is Outer<T>.E;       // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "e1 is Outer<T>.E").WithArguments("Outer<T>.E").WithLocation(26, 13),
                // (31,13): warning CS0183: The given expression is always of the provided ('Outer<int>.E') type
                //         b = e2 is Outer<int>.E;     // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "e2 is Outer<int>.E").WithArguments("Outer<int>.E").WithLocation(31, 13),
                // (32,13): warning CS0184: The given expression is never of the provided ('Outer<long>.E') type
                //         b = e2 is Outer<long>.E;    // Always false.
                Diagnostic(ErrorCode.WRN_IsAlwaysFalse, "e2 is Outer<long>.E").WithArguments("Outer<long>.E").WithLocation(32, 13));
        }

        [WorkItem(844635, "DevDiv")]
        [Fact()]
        public void TestIsOperatorWithTypesThatCannotUnify()
        {
            var source = @"
class Program
{
    static void Foo<T>(Outer<T>.S s1, Outer<T[]>.S s2)
    {
        bool b;
        b = s1 is Outer<int[]>.S;   // T -> int[]
        b = s1 is Outer<T[]>.S;     // Cannot unify - as in dev12, we do not warn.

        b = s2 is Outer<int[]>.S;   // T -> int
        b = s2 is Outer<T[]>.S;     // Always true.
        b = s2 is Outer<T[,]>.S;    // Cannot unify - as in dev12, we do not warn.
    }
}

class Outer<T>
{
    public struct S { }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (11,13): warning CS0183: The given expression is always of the provided ('Outer<T[]>.S') type
                //         b = s2 is Outer<T[]>.S;     // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "s2 is Outer<T[]>.S").WithArguments("Outer<T[]>.S").WithLocation(11, 13));
        }

        [WorkItem(844635, "DevDiv")]
        [Fact()]
        public void TestIsOperatorWithSpecialTypes()
        {
            var source = @"
using System;

class Program
{
    static void Foo<T, TClass, TStruct>(Outer<T>.E e1, Outer<int>.E e2, int i, T t, TClass tc, TStruct ts)
        where TClass : class
        where TStruct : struct
    {
        bool b;
        b = e1 is Enum; // Always true.
        b = e2 is Enum; // Always true.
        b = 0 is Enum;  // Always false.
        b = i is Enum;  // Always false.
        b = t is Enum;  // Deferred.
        b = tc is Enum; // Deferred.
        b = ts is Enum; // Deferred.

        b = e1 is ValueType; // Always true.
        b = e2 is ValueType; // Always true.
        b = 0 is ValueType;  // Always true.
        b = i is ValueType;  // Always true.
        b = t is ValueType;  // Deferred - null check.
        b = tc is ValueType; // Deferred - null check.
        b = ts is ValueType; // Always true.

        b = e1 is Object; // Always true.
        b = e2 is Object; // Always true.
        b = 0 is Object;  // Always true.
        b = i is Object;  // Always true.
        b = t is Object;  // Deferred - null check.
        b = tc is Object; // Deferred - null check.
        b = ts is Object; // Always true.
    }
}

class Outer<T>
{
    public enum E { }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (11,13): warning CS0183: The given expression is always of the provided ('System.Enum') type
                //         b = e1 is Enum; // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "e1 is Enum").WithArguments("System.Enum").WithLocation(11, 13),
                // (12,13): warning CS0183: The given expression is always of the provided ('System.Enum') type
                //         b = e2 is Enum; // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "e2 is Enum").WithArguments("System.Enum").WithLocation(12, 13),
                // (13,13): warning CS0184: The given expression is never of the provided ('System.Enum') type
                //         b = 0 is Enum;  // Always false.
                Diagnostic(ErrorCode.WRN_IsAlwaysFalse, "0 is Enum").WithArguments("System.Enum").WithLocation(13, 13),
                // (14,13): warning CS0184: The given expression is never of the provided ('System.Enum') type
                //         b = i is Enum;  // Always false.
                Diagnostic(ErrorCode.WRN_IsAlwaysFalse, "i is Enum").WithArguments("System.Enum").WithLocation(14, 13),

                // (19,13): warning CS0183: The given expression is always of the provided ('System.ValueType') type
                //         b = e1 is ValueType; // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "e1 is ValueType").WithArguments("System.ValueType").WithLocation(19, 13),
                // (20,13): warning CS0183: The given expression is always of the provided ('System.ValueType') type
                //         b = e2 is ValueType; // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "e2 is ValueType").WithArguments("System.ValueType").WithLocation(20, 13),
                // (21,13): warning CS0183: The given expression is always of the provided ('System.ValueType') type
                //         b = 0 is ValueType;  // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "0 is ValueType").WithArguments("System.ValueType").WithLocation(21, 13),
                // (22,13): warning CS0183: The given expression is always of the provided ('System.ValueType') type
                //         b = i is ValueType;  // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "i is ValueType").WithArguments("System.ValueType").WithLocation(22, 13),
                // (25,13): warning CS0183: The given expression is always of the provided ('System.ValueType') type
                //         b = ts is ValueType; // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "ts is ValueType").WithArguments("System.ValueType").WithLocation(25, 13),

                // (27,13): warning CS0183: The given expression is always of the provided ('object') type
                //         b = e1 is Object; // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "e1 is Object").WithArguments("object").WithLocation(27, 13),
                // (28,13): warning CS0183: The given expression is always of the provided ('object') type
                //         b = e2 is Object; // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "e2 is Object").WithArguments("object").WithLocation(28, 13),
                // (29,13): warning CS0183: The given expression is always of the provided ('object') type
                //         b = 0 is Object;  // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "0 is Object").WithArguments("object").WithLocation(29, 13),
                // (30,13): warning CS0183: The given expression is always of the provided ('object') type
                //         b = i is Object;  // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "i is Object").WithArguments("object").WithLocation(30, 13),
                // (33,13): warning CS0183: The given expression is always of the provided ('object') type
                //         b = ts is Object; // Always true.
                Diagnostic(ErrorCode.WRN_IsAlwaysTrue, "ts is Object").WithArguments("object").WithLocation(33, 13));
        }

        [WorkItem(543294, "DevDiv"), WorkItem(546655, "DevDiv")]
        [Fact()]
        public void TestAsOperator_SpecErrorCase()
        {
            // SPEC:    Furthermore, at least one of the following must be true, or otherwise a compile-time error occurs:
            // SPEC:    - An identity (�6.1.1), implicit nullable (�6.1.4), implicit reference (�6.1.6), boxing (�6.1.7), 
            // SPEC:        explicit nullable (�6.2.3), explicit reference (�6.2.4), or unboxing (�6.2.5) conversion exists
            // SPEC:        from E to T.
            // SPEC:    - The type of E or T is an open type.
            // SPEC:    - E is the null literal.

            // SPEC VIOLATION:  The specification contains an error in the list of legal conversions above.
            // SPEC VIOLATION:  If we have "class C<T, U> where T : U where U : class" then there is
            // SPEC VIOLATION:  an implicit conversion from T to U, but it is not an identity, reference or
            // SPEC VIOLATION:  boxing conversion. It will be one of those at runtime, but at compile time
            // SPEC VIOLATION:  we do not know which, and therefore cannot classify it as any of those.

            var source = @"
using System;

class Program
{
    static void Main()
    {
        Foo<Action, Action>(null);
    }

    static U Foo<T, U>(T t)
        where T : U
        where U : class
    {
        var s = t is U;
        return t as U;
    }
}
";
            
            CompileAndVerify(source, emitOptions: TestEmitters.CCI, expectedOutput: "").VerifyDiagnostics();
        }

        [WorkItem(546655, "DevDiv")]
        [Fact()]
        public void TestIsOperatorWithTypeParameter_Bug16461()
        {
            var source = @"
using System;

public class G<T>
{
    public bool M(T t) { return t is object; }
}

public class GG<T, V> where T : V
{
    public bool M(T t) { return t is V; }
}

class Test
{
    static void Main()
    {
      var obj = new G<Test>();
      Console.WriteLine(obj.M( (Test)null ));
 
      var obj1 = new GG<Test, Test>();
      Console.WriteLine(obj1.M( (Test)null ));
    }
}
";
            var comp = CompileAndVerify(source, emitOptions: TestEmitters.CCI, expectedOutput: @"False
False");
            comp.VerifyDiagnostics();
        }

        [Fact()]
        public void TestIsAsOperator_UserDefinedConversionsNotAllowed()
        {
            var source = @"
// conversion.cs

class Foo { public Foo(Bar b){} }

class Foo2 { public Foo2(Bar b){} }

struct Bar
{
    // Declare an implicit conversion from a int to a Bar
    static public implicit operator Bar(int value) 
    {
       return new Bar();
    }
    
    // Declare an explicit conversion from a Bar to Foo
    static public explicit operator Foo(Bar value)
    {
       return new Foo(value);
    }

    // Declare an implicit conversion from a Bar to Foo2
    static public implicit operator Foo2(Bar value)
    {
       return new Foo2(value);
    }
}

class Test
{
    static public void Main()
    {
        Bar numeral;

        numeral = 10;

        object a1 = numeral as Foo;
        object a2 = 1 as Bar;
        object a3 = numeral as Foo2;

        bool b1 = numeral is Foo;
        bool b2 = 1 is Bar;
        bool b3 = numeral is Foo2;
    }
}
";
            var comp = CreateCompilationWithMscorlib(source);
            comp.VerifyDiagnostics(
                // (37,21): error CS0039: Cannot convert type 'Bar' to 'Foo' via a reference conversion, boxing conversion, unboxing conversion, wrapping conversion, or null type conversion
                //         object a1 = numeral as Foo;
                Diagnostic(ErrorCode.ERR_NoExplicitBuiltinConv, "numeral as Foo").WithArguments("Bar", "Foo"),
                // (38,21): error CS0077: The as operator must be used with a reference type or nullable type ('Bar' is a non-nullable value type)
                //         object a2 = 1 as Bar;
                Diagnostic(ErrorCode.ERR_AsMustHaveReferenceType, "1 as Bar").WithArguments("Bar"),
                // (39,21): error CS0039: Cannot convert type 'Bar' to 'Foo2' via a reference conversion, boxing conversion, unboxing conversion, wrapping conversion, or null type conversion
                //         object a3 = numeral as Foo2;
                Diagnostic(ErrorCode.ERR_NoExplicitBuiltinConv, "numeral as Foo2").WithArguments("Bar", "Foo2"),
                // (41,19): warning CS0184: The given expression is never of the provided ('Foo') type
                //         bool b1 = numeral is Foo;
                Diagnostic(ErrorCode.WRN_IsAlwaysFalse, "numeral is Foo").WithArguments("Foo"),
                // (42,19): warning CS0184: The given expression is never of the provided ('Bar') type
                //         bool b2 = 1 is Bar;
                Diagnostic(ErrorCode.WRN_IsAlwaysFalse, "1 is Bar").WithArguments("Bar"),
                // (43,19): warning CS0184: The given expression is never of the provided ('Foo2') type
                //         bool b3 = numeral is Foo2;
                Diagnostic(ErrorCode.WRN_IsAlwaysFalse, "numeral is Foo2").WithArguments("Foo2"));
        }

        [WorkItem(543455, "DevDiv")]
        [Fact()]
        public void CS0184WRN_IsAlwaysFalse_Generic()
        {
            var text = @"
public class GenC<T> : GenI<T> where T : struct
{
    public bool Test(T t)
    {
        return (t is C);
    }
}
public interface GenI<T>
{
    bool Test(T t);
}
public class C
{
    public void Method() { }
    public static int Main()
    {
        return 0;
    }
}
";

            CreateCompilationWithMscorlib(text).VerifyDiagnostics(
                                Diagnostic(ErrorCode.WRN_IsAlwaysFalse, "t is C").WithArguments("C"));
        }

        [WorkItem(547011, "DevDiv")]
        [Fact()]
        public void CS0184WRN_IsAlwaysFalse_IntPtr()
        {
            var text = @"using System;
public enum E
{
  First
}

public class Base
{
    public static void Main()
    {
        E e = E.First;
        Console.WriteLine(e is IntPtr);
        Console.WriteLine(e as IntPtr);
    }
}
";

            CreateCompilationWithMscorlib(text).VerifyDiagnostics(
                // (12,27): warning CS0184: The given expression is never of the provided ('System.IntPtr') type
                //         Console.WriteLine(e is IntPtr);
                Diagnostic(ErrorCode.WRN_IsAlwaysFalse, "e is IntPtr").WithArguments("System.IntPtr"),
                // (13,27): error CS0077: The as operator must be used with a reference type or nullable type ('System.IntPtr' is a non-nullable value type)
                //         Console.WriteLine(e as IntPtr);
                Diagnostic(ErrorCode.ERR_AsMustHaveReferenceType, "e as IntPtr").WithArguments("System.IntPtr"));
        }

        [WorkItem(543443, "DevDiv")]
        [Fact]
        public void ParamsOperators()
        {
            var text =
@"class X
{
   public static bool operator >(X a, params int[] b)
    {
        return true;
    }
 
    public static bool operator <(X a, params int[] b)
    {
        return false;
    }
}";
            CreateCompilationWithMscorlib(text).VerifyDiagnostics(
                // (3,39): error CS1670: params is not valid in this context public static bool operator >(X a, params int[] b)
                Diagnostic(ErrorCode.ERR_IllegalParams, "params"),
                // (8,40): error CS1670: params is not valid in this context
                //     public static bool operator <(X a, params int[] b)
                Diagnostic(ErrorCode.ERR_IllegalParams, "params")
                );
        }

        [WorkItem(543438, "DevDiv")]
        [Fact()]
        public void TestNullCoalesce_UserDefinedConversions()
        {
            var text =
@"class B
{
    static void Main()
    {
        A a = null;
        B b = null;
        var c = a ?? b;
    }
}
 
class A
{
    public static implicit operator B(A x)
    {
        return new B();
    }
}";
            CompileAndVerify(text);
        }

        [WorkItem(543503, "DevDiv")]
        [Fact()]
        public void TestAsOperator_UserDefinedConversions()
        {
            var text =
@"using System;
 
class C<T>
{
    public static implicit operator string (C<T> x)
    {
        return """";
    }

    string s = new C<T>() as string;
}";
            CompileAndVerify(text);
        }

        [WorkItem(543503, "DevDiv")]
        [Fact()]
        public void TestIsOperator_UserDefinedConversions()
        {
            var text =
@"using System;
 
class C<T>
{
    public static implicit operator string (C<T> x)
    {
        return """";
    }
 
    bool b = new C<T>() is string;
}";
            CompileAndVerify(text);
        }

        [WorkItem(543483, "DevDiv")]
        [Fact]
        public void TestEqualityOperator_NullableStructs()
        {
            string source1 =
@"
public struct NonGenericStruct { }
public struct GenericStruct<T> { }

public class Foo
{
    public NonGenericStruct? ngsq;
    public GenericStruct<int>? gsiq;
}

public class GenFoo<T>
{
    public GenericStruct<T>? gstq;
}

public class Test
{
    public static bool Run()
    {
        Foo f = new Foo();
        f.ngsq = new NonGenericStruct();
        f.gsiq = new GenericStruct<int>();

        GenFoo<int> gf = new GenFoo<int>();
        gf.gstq = new GenericStruct<int>();

        return (f.ngsq != null) && (f.gsiq != null) && (gf.gstq != null);
    }
    public static void Main()
    {
        System.Console.WriteLine(Run() ? 1 : 0);
    }
}";

            string source2 = @"
struct S 
{
  public static bool operator ==(S? x, decimal? y) { return false; }
  public static bool operator !=(S? x, decimal? y) { return false; }
  public static bool operator ==(S? x, double? y) { return false; }
  public static bool operator !=(S? x, double? y) { return false; }
  public override int GetHashCode() { return 0; }
  public override bool Equals(object x) { return false; }
  static void Main()
  {
    S? s = default(S?);
    // This is *not* equivalent to !s.HasValue because
    // there is an applicable user-defined conversion.
    // Even though the conversion is ambiguous!
    if (s == null) s = default(S);
  }
}

";
            CompileAndVerify(source1, emitOptions: TestEmitters.CCI, expectedOutput: "1");
            CreateCompilationWithMscorlib(source2).VerifyDiagnostics(
// (16,9): error CS0034: Operator '==' is ambiguous on operands of type 'S?' and '<null>'
//     if (s == null) s = default(S);
Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "s == null").WithArguments("==", "S?", "<null>"));
        }

        [WorkItem(543432, "DevDiv")]
        [Fact]
        public void NoNewForOperators()
        {
            var text =
@"class A
{
    public static implicit operator A(D x)
    {
        return null;
    }
}
class B : A
{
    public static implicit operator B(D x)
    {
        return null;
    }
}
class D {}";
            CreateCompilationWithMscorlib(text).VerifyDiagnostics();
        }

        [Fact(),WorkItem(543433, "DevDiv")]
        public void ERR_NoImplicitConvCast_UserDefinedConversions()
        {
            var text =
@"class A
{
    public static A operator ++(A x)
    {
        return new A();
    }
}
 
class B : A
{
    static void Main()
    {
        B b = new B();
        b++;
    }
}
";
            CreateCompilationWithMscorlib(text).VerifyDiagnostics(Diagnostic(ErrorCode.ERR_NoImplicitConvCast, "b++").WithArguments("A", "B"));
        }

        [WorkItem(543431, "DevDiv")]
        [Fact]
        public void TestEqualityOperator_DelegateTypes_01()
        {
            string source =
@"
using System;
 
class C
{
    public static implicit operator Func<int>(C x)
    {
        return null;
    }
}
 
class D
{
    public static implicit operator Action(D x)
    {
        return null;
    }
 
    static void Main()
    {
        Console.WriteLine((C)null == (D)null);
        Console.WriteLine((C)null != (D)null);
    }
}
";
            string expectedOutput = @"True
False";
            CompileAndVerify(source, emitOptions: TestEmitters.CCI, expectedOutput: expectedOutput);
        }

        [WorkItem(543431, "DevDiv")]
        [Fact]
        public void TestEqualityOperator_DelegateTypes_02()
        {
            string source =
@"
using System;
 
class C
{
    public static implicit operator Func<int>(C x)
    {
        return null;
    }
}
 
class D
{
    public static implicit operator Action(D x)
    {
        return null;
    }
 
    static void Main()
    {
        Console.WriteLine((Func<int>)(C)null == (D)null);
        Console.WriteLine((Func<int>)(C)null == (Action)(D)null);
    }
}
";

            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (21,27): error CS0019: Operator '==' cannot be applied to operands of type 'System.Func<int>' and 'D'
                //         Console.WriteLine((Func<int>)(C)null == (D)null);
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "(Func<int>)(C)null == (D)null").WithArguments("==", "System.Func<int>", "D"),
                // (22,27): error CS0019: Operator '==' cannot be applied to operands of type 'System.Func<int>' and 'System.Action'
                //         Console.WriteLine((Func<int>)(C)null == (Action)(D)null);
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "(Func<int>)(C)null == (Action)(D)null").WithArguments("==", "System.Func<int>", "System.Action"));
        }

        [WorkItem(543431, "DevDiv")]
        [Fact]
        public void TestEqualityOperator_DelegateTypes_03_Ambiguous()
        {
            string source =
@"
using System;
 
class C
{
    public static implicit operator Func<int>(C x)
    {
        return null;
    }
}
 
class D
{
    public static implicit operator Action(D x)
    {
        return null;
    }

    public static implicit operator Func<int>(D x)
    {
        return null;
    }
 
    static void Main()
    {
        Console.WriteLine((C)null == (D)null);
        Console.WriteLine((C)null != (D)null);
    }
}
";

            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (26,27): error CS0019: Operator '==' cannot be applied to operands of type 'C' and 'D'
                //         Console.WriteLine((C)null == (D)null);
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "(C)null == (D)null").WithArguments("==", "C", "D"),
                // (27,27): error CS0019: Operator '!=' cannot be applied to operands of type 'C' and 'D'
                //         Console.WriteLine((C)null != (D)null);
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "(C)null != (D)null").WithArguments("!=", "C", "D"));
        }
 
        [WorkItem(543431, "DevDiv")]
        [Fact]
        public void TestEqualityOperator_DelegateTypes_04_BaseTypes()
        {
            string source =
@"
using System;

class A
{
    public static implicit operator Func<int>(A x)
    {
        return null;
    }
}

class C : A
{
}
 
class D
{
    public static implicit operator Func<int>(D x)
    {
        return null;
    }
 
    static void Main()
    {
        Console.WriteLine((C)null == (D)null);
        Console.WriteLine((C)null != (D)null);
    }
}
";
            string expectedOutput = @"True
False";
            CompileAndVerify(source, emitOptions: TestEmitters.CCI, expectedOutput: expectedOutput);
        }

        [WorkItem(543754, "DevDiv")]
        [Fact]
        public void TestEqualityOperator_NullableDecimal()
        {
            string source =
@"
public class Test
{
    public static bool Foo(decimal? deq)
    {
        return deq == null;
    }
    public static void Main()
    {
        Foo(null);
    }
}
";
            CompileAndVerify(source, expectedOutput: "");
        }

        [WorkItem(543910, "DevDiv")]
        [Fact]
        public void TypeParameterConstraintToGenericType()
        {
            string source =
@"
public class Gen<T>
{
	public T t;

	public Gen(T t)
	{
		this.t = t;
	}
	
	public static Gen<T> operator + (Gen<T> x, T y)
	{
		return new Gen<T>(y);
	}
}

public class ConstrainedTestContext<T,U> where T : Gen<U>
{
	public static Gen<U> ExecuteOpAddition(T x, U y)
	{
		return x + y;
	}
}
";

            CreateCompilationWithMscorlib(source).VerifyDiagnostics();
        }

        [WorkItem(544490, "DevDiv")]
        [Fact]
        public void LiftedUserDefinedUnaryOperator()
        {
            string source =
@"
struct S
{
    public static int operator +(S s) { return 1; }
    public static void Main()
    {
        S s = new S();
        S? sq = s;
        var j = +sq;
        System.Console.WriteLine(j);
    }
}
";
            CompileAndVerify(source, expectedOutput: "1");
        }

        [WorkItem(544490, "DevDiv")]
        [Fact]
        public void TestDefaultOperatorEnumConstantValue()
        {
            string source =
@"
enum X { F = 0 };
class C
{
    public static int Main()
    {
        const X x = default(X);
	    return (int)x;
    }
}
";
            CompileAndVerify(source, expectedOutput: "");
        }

        [Fact]
        public void OperatorHiding1()
        {
            string source = @"
class Base1
{
    public static Base1 operator +(Base1 b, Derived1 d) { return b; }
}

class Derived1 : Base1
{
    public static Base1 operator +(Base1 b, Derived1 d) { return b; }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics();
        }

        [Fact]
        public void OperatorHiding2()
        {
            string source = @"
class Base2
{
    public static Base2 op_Addition(Base2 b, Derived2 d) { return b; }
}

class Derived2 : Base2
{
    public static Base2 operator +(Base2 b, Derived2 d) { return b; }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics();
        }

        [Fact]
        public void OperatorHiding3()
        {
            string source = @"
class Base3
{
    public static Base3 operator +(Base3 b, Derived3 d) { return b; }
}

class Derived3 : Base3
{
    public static Base3 op_Addition(Base3 b, Derived3 d) { return b; }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics();
        }

        [Fact]
        public void OperatorHiding4()
        {
            string source = @"
class Base4
{
    public static Base4 op_Addition(Base4 b, Derived4 d) { return b; }
}

class Derived4 : Base4
{
    public static Base4 op_Addition(Base4 b, Derived4 d) { return b; }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (9,25): warning CS0108: 'Derived4.op_Addition(Base4, Derived4)' hides inherited member 'Base4.op_Addition(Base4, Derived4)'. Use the new keyword if hiding was intended.
                //     public static Base4 op_Addition(Base4 b, Derived4 d) { return b; }
                Diagnostic(ErrorCode.WRN_NewRequired, "op_Addition").WithArguments("Derived4.op_Addition(Base4, Derived4)", "Base4.op_Addition(Base4, Derived4)"));
        }

        [Fact]
        public void ConversionHiding1()
        {
            string source = @"
class Base1
{
    public static implicit operator string(Base1 b) { return null; }
}

class Derived1 : Base1
{
    public static implicit operator string(Base1 b) { return null; } // CS0556, but not CS0108
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (9,37): error CS0556: User-defined conversion must convert to or from the enclosing type
                //     public static implicit operator string(Base1 b) { return null; }
                Diagnostic(ErrorCode.ERR_ConversionNotInvolvingContainedType, "string"));
        }

        [Fact]
        public void ConversionHiding2()
        {
            string source = @"
class Base2
{
    public static string op_Explicit(Derived2 d) { return null; }
}

class Derived2 : Base2
{
    public static implicit operator string(Derived2 d) { return null; }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics();
        }

        [Fact]
        public void ConversionHiding3()
        {
            string source = @"
class Base3
{
    public static implicit operator string(Base3 b) { return null; }
}

class Derived3 : Base3
{
    public static string op_Explicit(Base3 b) { return null; }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics();
        }

        [Fact]
        public void ConversionHiding4()
        {
            string source = @"
class Base4
{
    public static string op_Explicit(Base4 b) { return null; }
}

class Derived4 : Base4
{
    public static string op_Explicit(Base4 b) { return null; }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (9,26): warning CS0108: 'Derived4.op_Explicit(Base4)' hides inherited member 'Base4.op_Explicit(Base4)'. Use the new keyword if hiding was intended.
                //     public static string op_Explicit(Base4 b) { return null; }
                Diagnostic(ErrorCode.WRN_NewRequired, "op_Explicit").WithArguments("Derived4.op_Explicit(Base4)", "Base4.op_Explicit(Base4)"));
        }

        [Fact]
        public void ClassesWithOperatorNames()
        {  
            string source = @"
class op_Increment
{
	public static op_Increment operator ++ (op_Increment c) { return null; }
}
class op_Decrement
{
	public static op_Decrement operator -- (op_Decrement c) { return null; }
}
class op_UnaryPlus
{
	public static int operator + (op_UnaryPlus c) { return 0; }
}
class op_UnaryNegation
{
	public static int operator - (op_UnaryNegation c) { return 0; }
}
class op_OnesComplement
{
	public static int operator ~ (op_OnesComplement c) { return 0; }
}
class op_Addition
{
	public static int operator + (op_Addition c, int i) { return 0; }
}
class op_Subtraction
{
	public static int operator - (op_Subtraction c, int i) { return 0; }
}
class op_Multiply
{
	public static int operator * (op_Multiply c, int i) { return 0; }
}
class op_Division
{
	public static int operator / (op_Division c, int i) { return 0; }
}
class op_Modulus
{
	public static int operator % (op_Modulus c, int i) { return 0; }
}
class op_ExclusiveOr
{
	public static int operator ^ (op_ExclusiveOr c, int i) { return 0; }
}
class op_BitwiseAnd
{
	public static int operator & (op_BitwiseAnd c, int i) { return 0; }
}
class op_BitwiseOr
{
	public static int operator | (op_BitwiseOr c, int i) { return 0; }
}
class op_LeftShift
{
	public static long operator <<  (op_LeftShift c, int i) { return 0; }
}
class op_RightShift
{
	public static long operator >>  (op_RightShift c, int i) { return 0; }
}
class op_UnsignedRightShift
{
		
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (4,38): error CS0542: 'op_Increment': member names cannot be the same as their enclosing type
                // 	public static op_Increment operator ++ (op_Increment c) { return null; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "++").WithArguments("op_Increment"),
                // (8,38): error CS0542: 'op_Decrement': member names cannot be the same as their enclosing type
                // 	public static op_Decrement operator -- (op_Decrement c) { return null; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "--").WithArguments("op_Decrement"),
                // (12,29): error CS0542: 'op_UnaryPlus': member names cannot be the same as their enclosing type
                // 	public static int operator + (op_UnaryPlus c) { return 0; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "+").WithArguments("op_UnaryPlus"),
                // (16,39): error CS0542: 'op_UnaryNegation': member names cannot be the same as their enclosing type
                // 	public static int operator - (op_UnaryNegation c) { return 0; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "-").WithArguments("op_UnaryNegation"),
                // (20,29): error CS0542: 'op_OnesComplement': member names cannot be the same as their enclosing type
                // 	public static int operator ~ (op_OnesComplement c) { return 0; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "~").WithArguments("op_OnesComplement"),
                // (24,29): error CS0542: 'op_Addition': member names cannot be the same as their enclosing type
                // 	public static int operator + (op_Addition c, int i) { return 0; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "+").WithArguments("op_Addition"),
                // (28,29): error CS0542: 'op_Subtraction': member names cannot be the same as their enclosing type
                // 	public static int operator - (op_Subtraction c, int i) { return 0; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "-").WithArguments("op_Subtraction"),
                // (32,29): error CS0542: 'op_Multiply': member names cannot be the same as their enclosing type
                // 	public static int operator * (op_Multiply c, int i) { return 0; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "*").WithArguments("op_Multiply"),
                // (36,29): error CS0542: 'op_Division': member names cannot be the same as their enclosing type
                // 	public static int operator / (op_Division c, int i) { return 0; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "/").WithArguments("op_Division"),
                // (40,29): error CS0542: 'op_Modulus': member names cannot be the same as their enclosing type
                // 	public static int operator % (op_Modulus c, int i) { return 0; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "%").WithArguments("op_Modulus"),
                // (44,29): error CS0542: 'op_ExclusiveOr': member names cannot be the same as their enclosing type
                // 	public static int operator ^ (op_ExclusiveOr c, int i) { return 0; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "^").WithArguments("op_ExclusiveOr"),
                // (48,29): error CS0542: 'op_BitwiseAnd': member names cannot be the same as their enclosing type
                // 	public static int operator & (op_BitwiseAnd c, int i) { return 0; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "&").WithArguments("op_BitwiseAnd"),
                // (52,29): error CS0542: 'op_BitwiseOr': member names cannot be the same as their enclosing type
                // 	public static int operator | (op_BitwiseOr c, int i) { return 0; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "|").WithArguments("op_BitwiseOr"),
                // (56,30): error CS0542: 'op_LeftShift': member names cannot be the same as their enclosing type
                // 	public static long operator <<  (op_LeftShift c, int i) { return 0; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "<<").WithArguments("op_LeftShift"),
                // (60,30): error CS0542: 'op_RightShift': member names cannot be the same as their enclosing type
                // 	public static long operator >>  (op_RightShift c, int i) { return 0; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, ">>").WithArguments("op_RightShift"));
        }

        [Fact]
        public void ClassesWithConversionNames()
        {
            string source = @"
class op_Explicit
{
    public static explicit operator op_Explicit(int x) { return null; }
}

class op_Implicit
{
    public static implicit operator op_Implicit(int x) { return null; }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (4,37): error CS0542: 'op_Explicit': member names cannot be the same as their enclosing type
                //     public static explicit operator op_Explicit(int x) { return null; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "op_Explicit").WithArguments("op_Explicit"),
                // (9,37): error CS0542: 'op_Implicit': member names cannot be the same as their enclosing type
                //     public static implicit operator op_Implicit(int x) { return null; }
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "op_Implicit").WithArguments("op_Implicit"));
        }

        [Fact, WorkItem(546771, "DevDiv")]
        public void TestIsNullable_Bug16777()
        {
            string source = @"
class Program
{
  enum E { }
  static void Main() 
  {
    M(null);
    M(0);
  }
  static void M(E? e)
  {
    System.Console.Write(e is E ? 't' : 'f');
  }
}
";

            CompileAndVerify(source: source, expectedOutput: "ft");
        }

        [Fact]
        public void CompoundOperatorWithThisOnLeft()
        {
            string source =
@"using System;
public struct Value
{
    int value;

    public Value(int value)
    {
        this.value = value;
    }

    public static Value operator +(Value a, int b)
    {
        return new Value(a.value + b);
    }

    public void Test()
    {
        this += 2;
    }

    public void Print()
    {
        Console.WriteLine(this.value);
    }

    public static void Main(string[] args)
    {
        Value v = new Value(1);
        v.Test();
        v.Print();
    }
}";
            string output = @"3";
            CompileAndVerify(source: source, expectedOutput: output);
        }

        [WorkItem(631414, "DevDiv")]
        [Fact]
        public void LiftedUserDefinedEquality1()
        {
            string source = @"
struct S1
{
    // Interesting
    public static bool operator ==(S1 x, S1 y) { throw null; }
    public static bool operator ==(S1 x, S2 y) { throw null; }

    // Filler
    public static bool operator !=(S1 x, S1 y) { throw null; }
    public static bool operator !=(S1 x, S2 y) { throw null; }
    public override bool Equals(object o) { throw null; }
    public override int GetHashCode() { throw null; }
}

struct S2
{
}

class Program
{
    bool Test(S1? s1)
    {
        return s1 == null;
    }
}
";
            var comp = CreateCompilationWithMscorlib(source);
            comp.VerifyDiagnostics();

            var expectedOperator = comp.GlobalNamespace.GetMember<NamedTypeSymbol>("S1").GetMembers(WellKnownMemberNames.EqualityOperatorName).
                OfType<MethodSymbol>().Single(m => m.ParameterTypes[0] == m.ParameterTypes[1]);

            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);

            var syntax = tree.GetRoot().DescendantNodes().OfType<BinaryExpressionSyntax>().Single();

            var info = model.GetSymbolInfo(syntax);
            Assert.Equal(expectedOperator, info.Symbol);
        }

        [WorkItem(631414, "DevDiv")]
        [Fact]
        public void LiftedUserDefinedEquality2()
        {
            string source = @"
using System;

struct S1
{
    // Interesting
    [Obsolete(""A"")]
    public static bool operator ==(S1 x, S1 y) { throw null; }
    [Obsolete(""B"")]
    public static bool operator ==(S1 x, S2 y) { throw null; }

    // Filler
    public static bool operator !=(S1 x, S1 y) { throw null; }
    public static bool operator !=(S1 x, S2 y) { throw null; }
    public override bool Equals(object o) { throw null; }
    public override int GetHashCode() { throw null; }
}

struct S2
{
}

class Program
{
    bool Test(S1? s1)
    {
        return s1 == null;
    }
}
";
            // CONSIDER: This is a little silly, since that method will never be called.
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (27,16): warning CS0618: 'S1.operator ==(S1, S1)' is obsolete: 'A'
                //         return s1 == null;
                Diagnostic(ErrorCode.WRN_DeprecatedSymbolStr, "s1 == null").WithArguments("S1.operator ==(S1, S1)", "A"));
        }

        [WorkItem(631414, "DevDiv")]
        [Fact]
        public void LiftedUserDefinedEquality3()
        {
            string source = @"
struct S1
{
    // Interesting
    public static bool operator ==(S1 x, S2 y) { throw null; }

    // Filler
    public static bool operator !=(S1 x, S2 y) { throw null; }
    public override bool Equals(object o) { throw null; }
    public override int GetHashCode() { throw null; }
}

struct S2
{
}

class Program
{
    bool Test(S1? s1, S2? s2)
    {
        return s1 == s2;
    }
}
";
            // CONSIDER: There is no reason not to allow this, but dev11 doesn't.
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (21,16): error CS0019: Operator '==' cannot be applied to operands of type 'S1?' and 'S2?'
                //         return s1 == s2;
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "s1 == s2").WithArguments("==", "S1?", "S2?"));
        }

        [WorkItem(656739, "DevDiv")]
        [Fact]
        public void AmbiguousLogicalOrConversion()
        {
            string source = @"
class InputParameter
{
    public static implicit operator bool(InputParameter inputParameter)
    {
        throw null;
    }

    public static implicit operator int(InputParameter inputParameter)
    {
        throw null;
    }
}

class Program
{
    static void Main(string[] args)
    {
        InputParameter i1 = new InputParameter();
        InputParameter i2 = new InputParameter();
        bool b = i1 || i2;
    }
}
";
            // SPEC VIOLATION: According to the spec, this is ambiguous.  However, we will match the dev11 behavior.
            var comp = CreateCompilationWithMscorlib(source);
            comp.VerifyDiagnostics();

            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);

            var syntax = tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>().Last();
            Assert.Equal("i2", syntax.Identifier.ValueText);

            var info = model.GetTypeInfo(syntax);
            Assert.Equal(comp.GlobalNamespace.GetMember<NamedTypeSymbol>("InputParameter"), info.Type);
            Assert.Equal(comp.GetSpecialType(SpecialType.System_Boolean), info.ConvertedType);
        }

        [WorkItem(656739, "DevDiv")]
        [Fact]
        public void AmbiguousOrConversion()
        {
            string source = @"
class InputParameter
{
    public static implicit operator bool(InputParameter inputParameter)
    {
        throw null;
    }

    public static implicit operator int(InputParameter inputParameter)
    {
        throw null;
    }
}

class Program
{
    static void Main(string[] args)
    {
        InputParameter i1 = new InputParameter();
        InputParameter i2 = new InputParameter();
        bool b = i1 | i2;
    }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (21,18): error CS0034: Operator '|' is ambiguous on operands of type 'InputParameter' and 'InputParameter'
                //         bool b = i1 | i2;
                Diagnostic(ErrorCode.ERR_AmbigBinaryOps, "i1 | i2").WithArguments("|", "InputParameter", "InputParameter"));
        }

        [WorkItem(656739, "DevDiv")]
        [Fact]
        public void DynamicAmbiguousLogicalOrConversion()
        {
            string source = @"
using System;

class InputParameter
{
    public static implicit operator bool(InputParameter inputParameter)
    {
        System.Console.WriteLine(""A"");
        return true;
    }

    public static implicit operator int(InputParameter inputParameter)
    {
        System.Console.WriteLine(""B"");
        return 1;
    }
}

class Program
{
    static void Main(string[] args)
    {
        dynamic i1 = new InputParameter();
        dynamic i2 = new InputParameter();
        bool b = i1 || i2;
    }
}
";
            var comp = CreateCompilationWithMscorlib(source, new[] { SystemCoreRef, CSharpRef }, TestOptions.ReleaseExe);
            CompileAndVerify(comp, expectedOutput: @"A
A");
        }

        [WorkItem(656739, "DevDiv")]
        [Fact]
        public void DynamicAmbiguousOrConversion()
        {
            string source = @"
using System;

class InputParameter
{
    public static implicit operator bool(InputParameter inputParameter)
    {
        System.Console.WriteLine(""A"");
        return true;
    }

    public static implicit operator int(InputParameter inputParameter)
    {
        System.Console.WriteLine(""B"");
        return 1;
    }
}

class Program
{
    static void Main(string[] args)
    {
        dynamic i1 = new InputParameter();
        dynamic i2 = new InputParameter();
        bool b = i1 | i2;
    }
}
";
            var comp = CreateCompilationWithMscorlib(source, new[] { SystemCoreRef, CSharpRef }, TestOptions.ReleaseExe);
            CompileAndVerifyException<Microsoft.CSharp.RuntimeBinder.RuntimeBinderException>(comp, 
                "Operator '|' is ambiguous on operands of type 'InputParameter' and 'InputParameter'");
        }

        [WorkItem(656739, "DevDiv")]
        [Fact]
        public void UnambiguousLogicalOrConversion1()
        {
            string source = @"
class InputParameter
{
    public static implicit operator bool(InputParameter inputParameter)
    {
        throw null;
    }
}

class Program
{
    static void Main(string[] args)
    {
        InputParameter i1 = new InputParameter();
        InputParameter i2 = new InputParameter();
        bool b = i1 || i2;
    }
}
";
            var comp = CreateCompilationWithMscorlib(source);
            comp.VerifyDiagnostics();

            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);

            var syntax = tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>().Last();
            Assert.Equal("i2", syntax.Identifier.ValueText);

            var info = model.GetTypeInfo(syntax);
            Assert.Equal(comp.GlobalNamespace.GetMember<NamedTypeSymbol>("InputParameter"), info.Type);
            Assert.Equal(comp.GetSpecialType(SpecialType.System_Boolean), info.ConvertedType);
        }

        [WorkItem(656739, "DevDiv")]
        [Fact]
        public void UnambiguousLogicalOrConversion2()
        {
            string source = @"
class InputParameter
{
    public static implicit operator int(InputParameter inputParameter)
    {
        throw null;
    }
}

class Program
{
    static void Main(string[] args)
    {
        InputParameter i1 = new InputParameter();
        InputParameter i2 = new InputParameter();
        bool b = i1 || i2;
    }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (16,18): error CS0019: Operator '||' cannot be applied to operands of type 'InputParameter' and 'InputParameter'
                //         bool b = i1 || i2;
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "i1 || i2").WithArguments("||", "InputParameter", "InputParameter"));
        }

        [WorkItem(665002, "DevDiv")]
        [Fact]
        public void DedupingLiftedUserDefinedOperators()
        {
            string source = @"
using System;
public class RubyTime
{
    public static TimeSpan operator -(RubyTime x, DateTime y)
    {
        throw null;
    }
    public static TimeSpan operator -(RubyTime x, RubyTime y)
    {
        throw null;
    }
    public static implicit operator DateTime(RubyTime time)
    {
        throw null;
    }

    TimeSpan Test(RubyTime x, DateTime y)
    {
        return x - y;
    }
}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics();
        }

        [Fact()]
        public void UnaryIntrinsicSymbols1()
        {
            UnaryOperatorKind[] operators = 
            {
            UnaryOperatorKind.PostfixIncrement,
            UnaryOperatorKind.PostfixDecrement,
            UnaryOperatorKind.PrefixIncrement,
            UnaryOperatorKind.PrefixDecrement,
            UnaryOperatorKind.UnaryPlus,
            UnaryOperatorKind.UnaryMinus,
            UnaryOperatorKind.LogicalNegation,
            UnaryOperatorKind.BitwiseComplement 
            };

            string[] opTokens = {"++","--","++","--",
                                 "+","-","!","~"};

            string[] typeNames =
                {
                "System.Object",
                "System.String",
                "System.Double",
                "System.SByte",
                "System.Int16",
                "System.Int32",
                "System.Int64",
                "System.Decimal",
                "System.Single",
                "System.Byte",
                "System.UInt16",
                "System.UInt32",
                "System.UInt64",
                "System.Boolean",
                "System.Char",
                "System.DateTime",
                "System.TypeCode",
                "System.StringComparison",
                "System.Guid",
                "dynamic",
                "byte*"
                };

            var builder = new System.Text.StringBuilder();
            int n = 0;

                builder.Append(
"class Module1\n"+
"{\n");

            foreach (var arg1 in typeNames)
            {
                n += 1;
                builder.AppendFormat(
"void Test{1}({0} x1, System.Nullable<{0}> x2)\n", arg1, n);
                builder.Append(
"{\n");

                for (int k = 0; k < operators.Length; k++)
                {
                    if (operators[k] == UnaryOperatorKind.PostfixDecrement || operators[k] == UnaryOperatorKind.PostfixIncrement)
                    {
                        builder.AppendFormat(
    "    var z{0}_1 = x1 {1};\n" +
    "    var z{0}_2 = x2 {1};\n" +
    "    if (x1 {1}) {{}}\n" +
    "    if (x2 {1}) {{}}\n",
                                             k, opTokens[k]);
                    }
                    else
                    {
                        builder.AppendFormat(
    "    var z{0}_1 = {1} x1;\n" +
    "    var z{0}_2 = {1} x2;\n" +
    "    if ({1} x1) {{}}\n" +
    "    if ({1} x2) {{}}\n",
                                             k, opTokens[k]);
                    }
                }

                builder.Append(
"}\n");
            }

                builder.Append(
"}\n");

            var source = builder.ToString();

            var compilation = CreateCompilationWithMscorlib(source, options: TestOptions.ReleaseDll.WithOverflowChecks(true));

            var tree = compilation.SyntaxTrees.Single();
            var semanticModel = compilation.GetSemanticModel(tree);

            var nodes = (from node in tree.GetRoot().DescendantNodes()
                         select ((ExpressionSyntax)(node as PrefixUnaryExpressionSyntax)) ?? node as PostfixUnaryExpressionSyntax).
                         Where(node => (object)node != null).ToArray();

            n = 0;
            for (int name = 0; name < typeNames.Length; name++)
            {
                TypeSymbol type;

                if (name == typeNames.Length - 1)
                {
                    type = compilation.CreatePointerTypeSymbol(compilation.GetSpecialType(SpecialType.System_Byte));
                }
                else if (name == typeNames.Length - 2)
                {
                    type = compilation.DynamicType;
                }
                else
                {
                    type = compilation.GetTypeByMetadataName(typeNames[name]);
                }

                foreach (var op in operators)
                {
                    TestUnaryIntrinsicSymbol(
                        op,
                        type,
                        compilation,
                        semanticModel,
                        nodes[n],
                        nodes[n + 1],
                        nodes[n + 2],
                        nodes[n + 3]);
                    n += 4;
                }
            }

            Assert.Equal(n, nodes.Length);
        }

        private void TestUnaryIntrinsicSymbol(
            UnaryOperatorKind op,
            TypeSymbol type,
            CSharpCompilation compilation,
            SemanticModel semanticModel,
            ExpressionSyntax node1,
            ExpressionSyntax node2,
            ExpressionSyntax node3,
            ExpressionSyntax node4
        )
        {
            SymbolInfo info1 = semanticModel.GetSymbolInfo(node1);
            Assert.Equal(type.IsDynamic() ? CandidateReason.LateBound : CandidateReason.None, info1.CandidateReason);
            Assert.Equal(0, info1.CandidateSymbols.Length);

            var symbol1 = (MethodSymbol)info1.Symbol;
            var symbol2 = semanticModel.GetSymbolInfo(node2).Symbol;
            var symbol3 = (MethodSymbol)semanticModel.GetSymbolInfo(node3).Symbol;
            var symbol4 = semanticModel.GetSymbolInfo(node4).Symbol;

            Assert.Equal(symbol1, symbol3);

            if ((object)symbol1 != null)
            {
                Assert.NotSame(symbol1, symbol3);
                Assert.Equal(symbol1.GetHashCode(), symbol3.GetHashCode());

                Assert.Equal(symbol1.Parameters[0], symbol3.Parameters[0]);
                Assert.Equal(symbol1.Parameters[0].GetHashCode(), symbol3.Parameters[0].GetHashCode());
            }

            Assert.Equal(symbol2, symbol4);

            TypeSymbol underlying = type;

            if (op == UnaryOperatorKind.BitwiseComplement ||
                op == UnaryOperatorKind.PrefixDecrement || op == UnaryOperatorKind.PrefixIncrement ||
                op == UnaryOperatorKind.PostfixDecrement || op == UnaryOperatorKind.PostfixIncrement)
            {
                underlying = type.EnumUnderlyingType();
            }

            UnaryOperatorKind result = OverloadResolution.UnopEasyOut.OpKind(op, underlying);
            UnaryOperatorSignature signature;

            if (result == UnaryOperatorKind.Error)
            {
                if (type.IsDynamic())
                {
                    signature = new UnaryOperatorSignature(op | UnaryOperatorKind.Dynamic, type, type);
                }
                else if (type.IsPointerType() &&
                    (op == UnaryOperatorKind.PrefixDecrement || op == UnaryOperatorKind.PrefixIncrement ||
                        op == UnaryOperatorKind.PostfixDecrement || op == UnaryOperatorKind.PostfixIncrement))
                {
                    signature = new UnaryOperatorSignature(op | UnaryOperatorKind.Pointer, type, type);
                }
                else
                {
                    Assert.Null(symbol1);
                    Assert.Null(symbol2);
                    Assert.Null(symbol3);
                    Assert.Null(symbol4);
                    return;
                }
            }
            else
            {
                signature = compilation.builtInOperators.GetSignature(result);

                if ((object)underlying != (object)type)
                {
                    Assert.Equal(underlying, signature.OperandType);
                    Assert.Equal(underlying, signature.ReturnType);

                    signature = new UnaryOperatorSignature(signature.Kind, type, type);
                }
            }

            Assert.NotNull(symbol1);

            string containerName = signature.OperandType.ToTestDisplayString();
            string returnName = signature.ReturnType.ToTestDisplayString();

            if (op == UnaryOperatorKind.LogicalNegation && type.IsEnumType())
            {
                containerName = type.ToTestDisplayString();
                returnName = containerName;
            }

            Assert.Equal(String.Format("{2} {0}.{1}({0} value)",
                                       containerName,
                                       OperatorFacts.UnaryOperatorNameFromOperatorKind(op),
                                       returnName),
                         symbol1.ToTestDisplayString());

            Assert.Equal(MethodKind.BuiltinOperator, symbol1.MethodKind);
            Assert.True(symbol1.IsImplicitlyDeclared);

            bool expectChecked = false;

            switch (op)
            {
                case UnaryOperatorKind.UnaryMinus:
                    expectChecked = (type.IsDynamic() || symbol1.ContainingType.EnumUnderlyingType().SpecialType.IsIntegralType());
                    break;

                case UnaryOperatorKind.PrefixDecrement: 
                case UnaryOperatorKind.PrefixIncrement:
                case UnaryOperatorKind.PostfixDecrement:
                case UnaryOperatorKind.PostfixIncrement:
                    expectChecked = (type.IsDynamic() || type.IsPointerType() ||
                                     symbol1.ContainingType.EnumUnderlyingType().SpecialType.IsIntegralType() || 
                                     symbol1.ContainingType.SpecialType == SpecialType.System_Char);
                    break;

                default:
                    expectChecked = type.IsDynamic();
                    break;
            }

            Assert.Equal(expectChecked, symbol1.IsCheckedBuiltin);

            Assert.False(symbol1.IsGenericMethod);
            Assert.False(symbol1.IsExtensionMethod);
            Assert.False(symbol1.IsExtern);
            Assert.False(symbol1.CanBeReferencedByName);
            Assert.Null(symbol1.DeclaringCompilation);
            Assert.Equal(symbol1.Name, symbol1.MetadataName);
            Assert.Same(symbol1.ContainingSymbol, symbol1.Parameters[0].Type);
            Assert.Equal(0, symbol1.Locations.Length);
            Assert.Null(symbol1.GetDocumentationCommentId());
            Assert.Equal("", symbol1.GetDocumentationCommentXml());

            Assert.True(symbol1.HasSpecialName);
            Assert.True(symbol1.IsStatic);
            Assert.Equal(Accessibility.Public, symbol1.DeclaredAccessibility);
            Assert.False(symbol1.HidesBaseMethodsByName);
            Assert.False(symbol1.IsOverride);
            Assert.False(symbol1.IsVirtual);
            Assert.False(symbol1.IsAbstract);
            Assert.False(symbol1.IsSealed);
            Assert.Equal(1, symbol1.ParameterCount);
            Assert.Equal(0, symbol1.Parameters[0].Ordinal);

            var otherSymbol = (MethodSymbol)semanticModel.GetSymbolInfo(node1).Symbol;
            Assert.Equal(symbol1, otherSymbol);

            if (type.IsValueType && !type.IsPointerType())
            {
                Assert.Equal(symbol1, symbol2);
                return;
            }

            Assert.Null(symbol2);
        }

        [Fact()]
        public void CheckedUnaryIntrinsicSymbols()
        {
            var source =
@"
class Module1
{
    void Test(int x)
    {
        var z1 = -x;
        var z2 = --x;
    }
}";

            var compilation = CreateCompilationWithMscorlib(source, options:TestOptions.ReleaseDll.WithOverflowChecks(false));

            var tree = compilation.SyntaxTrees.Single();
            var semanticModel = compilation.GetSemanticModel(tree);

            var nodes = (from node in tree.GetRoot().DescendantNodes()
                         select ((ExpressionSyntax)(node as PrefixUnaryExpressionSyntax)) ?? node as PostfixUnaryExpressionSyntax).
                         Where(node => (object)node != null).ToArray();

            Assert.Equal(2, nodes.Length);

            var symbols1 = (from node1 in nodes select (MethodSymbol)semanticModel.GetSymbolInfo(node1).Symbol).ToArray();
            foreach (var symbol1 in symbols1)
            {
                Assert.False(symbol1.IsCheckedBuiltin);
            }

            compilation = compilation.WithOptions(TestOptions.ReleaseDll.WithOverflowChecks(true));
            semanticModel = compilation.GetSemanticModel(tree);

            var symbols2 = (from node2 in nodes select (MethodSymbol)semanticModel.GetSymbolInfo(node2).Symbol).ToArray();
            foreach (var symbol2 in symbols2)
            {
                Assert.True(symbol2.IsCheckedBuiltin);
            }

            for (int i = 0; i < symbols1.Length; i++)
            {
                Assert.NotEqual(symbols1[i], symbols2[i]);
            }
        }

        [Fact()]
        public void BinaryIntrinsicSymbols1()
        {
            BinaryOperatorKind[] operators =
                        {
                        BinaryOperatorKind.Addition,
                        BinaryOperatorKind.Subtraction,
                        BinaryOperatorKind.Multiplication,
                        BinaryOperatorKind.Division,
                        BinaryOperatorKind.Remainder,
                        BinaryOperatorKind.Equal,
                        BinaryOperatorKind.NotEqual,
                        BinaryOperatorKind.LessThanOrEqual,
                        BinaryOperatorKind.GreaterThanOrEqual,
                        BinaryOperatorKind.LessThan,
                        BinaryOperatorKind.GreaterThan,
                        BinaryOperatorKind.LeftShift,
                        BinaryOperatorKind.RightShift,
                        BinaryOperatorKind.Xor,
                        BinaryOperatorKind.Or,
                        BinaryOperatorKind.And,
                        BinaryOperatorKind.LogicalOr,
                        BinaryOperatorKind.LogicalAnd
                        };

            string[] opTokens = {
                                 "+",
                                 "-",
                                 "*",
                                 "/",
                                 "%",
                                 "==",
                                 "!=",
                                 "<=",
                                 ">=",
                                 "<",
                                 ">",
                                 "<<",
                                 ">>",
                                 "^",
                                 "|",
                                 "&",
                                 "||",
                                 "&&"};

            string[] typeNames =
                            {
                            "System.Object",
                            "System.String",
                            "System.Double",
                            "System.SByte",
                            "System.Int16",
                            "System.Int32",
                            "System.Int64",
                            "System.Decimal",
                            "System.Single",
                            "System.Byte",
                            "System.UInt16",
                            "System.UInt32",
                            "System.UInt64",
                            "System.Boolean",
                            "System.Char",
                            "System.DateTime",
                            "System.TypeCode",
                            "System.StringComparison",
                            "System.Guid",
                            "System.Delegate",
                            "System.Action",
                            "System.AppDomainInitializer",
                            "System.ValueType",
                            "TestStructure",
                            "Module1",
                            "dynamic",
                            "byte*",
                            "sbyte*"
                            };

            var builder = new System.Text.StringBuilder();
            int n = 0;

            builder.Append(
"struct TestStructure\n" +
"{}\n" +
"class Module1\n"+
"{\n");

            foreach (var arg1 in typeNames)
            {
                foreach (var arg2 in typeNames)
                {
                    n += 1;
                    builder.AppendFormat(
"void Test{2}({0} x1, {1} y1, System.Nullable<{0}> x2, System.Nullable<{1}> y2)\n" +
"{{\n", arg1, arg2, n);

                    for (int k = 0; k < opTokens.Length; k++)
                    {
                        builder.AppendFormat(
"    var z{0}_1 = x1 {1} y1;\n" +
"    var z{0}_2 = x2 {1} y2;\n" +
"    var z{0}_3 = x2 {1} y1;\n" +
"    var z{0}_4 = x1 {1} y2;\n" +
"    if (x1 {1} y1) {{}}\n" +
"    if (x2 {1} y2) {{}}\n" +
"    if (x2 {1} y1) {{}}\n" +
"    if (x1 {1} y2) {{}}\n",
                                                k, opTokens[k]);
                    }

                    builder.Append(
"}\n");
                }
            }

            builder.Append(
"}\n");

            var source = builder.ToString();

            var compilation = CreateCompilationWithMscorlib(source, options: TestOptions.ReleaseDll.WithOverflowChecks(true));

            var tree = compilation.SyntaxTrees.Single();
            var semanticModel = compilation.GetSemanticModel(tree);

            TypeSymbol [] types = new TypeSymbol[typeNames.Length];

            for (int i = 0; i < typeNames.Length - 3; i++)
            {
                types[i] = compilation.GetTypeByMetadataName(typeNames[i]);
            }

            Assert.Null(types[types.Length - 3]);
            types[types.Length - 3] = compilation.DynamicType;

            Assert.Null(types[types.Length - 2]);
            types[types.Length - 2] = compilation.CreatePointerTypeSymbol(compilation.GetSpecialType(SpecialType.System_Byte));

            Assert.Null(types[types.Length - 1]);
            types[types.Length - 1] = compilation.CreatePointerTypeSymbol(compilation.GetSpecialType(SpecialType.System_SByte));

            var nodes = (from node in tree.GetRoot().DescendantNodes()
                         select (node as BinaryExpressionSyntax)).
                         Where(node => (object)node != null).ToArray();

            n = 0;
            foreach (var leftType in types)
            {
                foreach (var rightType in types)
                {
                    foreach (var op in operators)
                    {
                        TestBinaryIntrinsicSymbol(
                            op,
                            leftType,
                            rightType,
                            compilation,
                            semanticModel,
                            nodes[n],
                            nodes[n + 1],
                            nodes[n + 2],
                            nodes[n + 3],
                            nodes[n + 4],
                            nodes[n + 5],
                            nodes[n + 6],
                            nodes[n + 7]);
                        n += 8;
                    }
                }
            }

            Assert.Equal(n, nodes.Length);
        }

        [Fact()]
        public void BinaryIntrinsicSymbols2()
        {
            BinaryOperatorKind[] operators =
                        {
                        BinaryOperatorKind.Addition,
                        BinaryOperatorKind.Subtraction,
                        BinaryOperatorKind.Multiplication,
                        BinaryOperatorKind.Division,
                        BinaryOperatorKind.Remainder,
                        BinaryOperatorKind.LeftShift,
                        BinaryOperatorKind.RightShift,
                        BinaryOperatorKind.Xor,
                        BinaryOperatorKind.Or,
                        BinaryOperatorKind.And
                        };

            string[] opTokens = {
                                 "+=",
                                 "-=",
                                 "*=",
                                 "/=",
                                 "%=",
                                 "<<=",
                                 ">>=",
                                 "^=",
                                 "|=",
                                 "&="};

            string[] typeNames =
                            {
                            "System.Object",
                            "System.String",
                            "System.Double",
                            "System.SByte",
                            "System.Int16",
                            "System.Int32",
                            "System.Int64",
                            "System.Decimal",
                            "System.Single",
                            "System.Byte",
                            "System.UInt16",
                            "System.UInt32",
                            "System.UInt64",
                            "System.Boolean",
                            "System.Char",
                            "System.DateTime",
                            "System.TypeCode",
                            "System.StringComparison",
                            "System.Guid",
                            "System.Delegate",
                            "System.Action",
                            "System.AppDomainInitializer",
                            "System.ValueType",
                            "TestStructure",
                            "Module1",
                            "dynamic",
                            "byte*",
                            "sbyte*"
                            };

            var builder = new System.Text.StringBuilder();
            int n = 0;

            builder.Append(
"struct TestStructure\n" +
"{}\n" +
"class Module1\n" +
"{\n");

            foreach (var arg1 in typeNames)
            {
                foreach (var arg2 in typeNames)
                {
                    n += 1;
                    builder.AppendFormat(
"void Test{2}({0} x1, {1} y1, System.Nullable<{0}> x2, System.Nullable<{1}> y2)\n" +
"{{\n", arg1, arg2, n);

                    for (int k = 0; k < opTokens.Length; k++)
                    {
                        builder.AppendFormat(
"    x1 {1} y1;\n" +
"    x2 {1} y2;\n" +
"    x2 {1} y1;\n" +
"    x1 {1} y2;\n" +
"    if (x1 {1} y1) {{}}\n" +
"    if (x2 {1} y2) {{}}\n" +
"    if (x2 {1} y1) {{}}\n" +
"    if (x1 {1} y2) {{}}\n",
                                                k, opTokens[k]);
                    }

                    builder.Append(
"}\n");
                }
            }

            builder.Append(
"}\n");

            var source = builder.ToString();

            var compilation = CreateCompilationWithMscorlib(source, options: TestOptions.ReleaseDll.WithOverflowChecks(true));

            var tree = compilation.SyntaxTrees.Single();
            var semanticModel = compilation.GetSemanticModel(tree);

            TypeSymbol[] types = new TypeSymbol[typeNames.Length];

            for (int i = 0; i < typeNames.Length - 3; i++)
            {
                types[i] = compilation.GetTypeByMetadataName(typeNames[i]);
            }

            Assert.Null(types[types.Length - 3]);
            types[types.Length - 3] = compilation.DynamicType;

            Assert.Null(types[types.Length - 2]);
            types[types.Length - 2] = compilation.CreatePointerTypeSymbol(compilation.GetSpecialType(SpecialType.System_Byte));

            Assert.Null(types[types.Length - 1]);
            types[types.Length - 1] = compilation.CreatePointerTypeSymbol(compilation.GetSpecialType(SpecialType.System_SByte));

            var nodes = (from node in tree.GetRoot().DescendantNodes()
                         select (node as AssignmentExpressionSyntax)).
                         Where(node => (object)node != null).ToArray();

            n = 0;
            foreach (var leftType in types)
            {
                foreach (var rightType in types)
                {
                    foreach (var op in operators)
                    {
                        TestBinaryIntrinsicSymbol(
                            op,
                            leftType,
                            rightType,
                            compilation,
                            semanticModel,
                            nodes[n],
                            nodes[n + 1],
                            nodes[n + 2],
                            nodes[n + 3],
                            nodes[n + 4],
                            nodes[n + 5],
                            nodes[n + 6],
                            nodes[n + 7]);
                        n += 8;
                    }
                }
            }

            Assert.Equal(n, nodes.Length);
        }

        private void TestBinaryIntrinsicSymbol(
            BinaryOperatorKind op,
            TypeSymbol leftType,
            TypeSymbol rightType,
            CSharpCompilation compilation,
            SemanticModel semanticModel,
            ExpressionSyntax node1,
            ExpressionSyntax node2,
            ExpressionSyntax node3,
            ExpressionSyntax node4,
            ExpressionSyntax node5,
            ExpressionSyntax node6,
            ExpressionSyntax node7,
            ExpressionSyntax node8
        )
        {
            SymbolInfo info1 = semanticModel.GetSymbolInfo(node1);
            HashSet<DiagnosticInfo> useSiteDiagnostics = null;

            if (info1.Symbol == null)
            {
                if (info1.CandidateSymbols.Length == 0)
                {
                    if (leftType.IsDynamic() || rightType.IsDynamic())
                    {
                        Assert.True(CandidateReason.LateBound == info1.CandidateReason || CandidateReason.None == info1.CandidateReason);
                    }
                    else
                    {
                        Assert.Equal(CandidateReason.None, info1.CandidateReason);
                    }
                }
                else
                {
                    Assert.Equal(CandidateReason.OverloadResolutionFailure, info1.CandidateReason);
                    foreach (MethodSymbol s in info1.CandidateSymbols)
                    {
                        Assert.Equal(MethodKind.UserDefinedOperator, s.MethodKind);
                    }
                }
            }
            else
            {
                Assert.Equal(leftType.IsDynamic() || rightType.IsDynamic() ? CandidateReason.LateBound : CandidateReason.None, info1.CandidateReason);
                Assert.Equal(0, info1.CandidateSymbols.Length);
            }

            var symbol1 = (MethodSymbol)info1.Symbol;
            var symbol2 = semanticModel.GetSymbolInfo(node2).Symbol;
            var symbol3 = semanticModel.GetSymbolInfo(node3).Symbol;
            var symbol4 = semanticModel.GetSymbolInfo(node4).Symbol;
            var symbol5 = (MethodSymbol)semanticModel.GetSymbolInfo(node5).Symbol;
            var symbol6 = semanticModel.GetSymbolInfo(node6).Symbol;
            var symbol7 = semanticModel.GetSymbolInfo(node7).Symbol;
            var symbol8 = semanticModel.GetSymbolInfo(node8).Symbol;

            Assert.Equal(symbol1, symbol5);
            Assert.Equal(symbol2, symbol6);
            Assert.Equal(symbol3, symbol7);
            Assert.Equal(symbol4, symbol8);

            if ((object)symbol1 != null && symbol1.IsImplicitlyDeclared)
            {
                Assert.NotSame(symbol1, symbol5);
                Assert.Equal(symbol1.GetHashCode(), symbol5.GetHashCode());

                for (int i = 0; i < 2; i++)
                {
                    Assert.Equal(symbol1.Parameters[i], symbol5.Parameters[i]);
                    Assert.Equal(symbol1.Parameters[i].GetHashCode(), symbol5.Parameters[i].GetHashCode());
                }

                Assert.NotEqual(symbol1.Parameters[0], symbol5.Parameters[1]);
            }

            switch (op)
            {
                case BinaryOperatorKind.LogicalAnd:
                case BinaryOperatorKind.LogicalOr:
                    Assert.Null(symbol1);
                    Assert.Null(symbol2);
                    Assert.Null(symbol3);
                    Assert.Null(symbol4);
                    return;
            }


            BinaryOperatorKind result = OverloadResolution.BinopEasyOut.OpKind(op, leftType, rightType);
            BinaryOperatorSignature signature;
            bool isDynamic = (leftType.IsDynamic() || rightType.IsDynamic());

            if (result == BinaryOperatorKind.Error)
            {
                if (leftType.IsDynamic() && !rightType.IsPointerType() && !rightType.IsRestrictedType())
                {
                    signature = new BinaryOperatorSignature(op | BinaryOperatorKind.Dynamic,leftType,rightType,leftType);
                }
                else if (rightType.IsDynamic() && !leftType.IsPointerType() && !leftType.IsRestrictedType())
                {
                    signature = new BinaryOperatorSignature(op | BinaryOperatorKind.Dynamic, leftType, rightType, rightType);
                }
                else if ((op == BinaryOperatorKind.Equal || op == BinaryOperatorKind.NotEqual) && 
                    leftType.IsReferenceType && rightType.IsReferenceType &&
                    (leftType == rightType || compilation.Conversions.ClassifyConversion(leftType, rightType, ref useSiteDiagnostics).IsReference))
                {

                    if (leftType.IsDelegateType() && rightType.IsDelegateType())
                    {
                        Assert.Equal(leftType, rightType);
                        signature = new BinaryOperatorSignature(op | BinaryOperatorKind.Delegate,
                            leftType, // TODO: this feels like a spec violation
                            leftType, // TODO: this feels like a spec violation
                            compilation.GetSpecialType(SpecialType.System_Boolean));
                    }
                    else if (leftType.SpecialType == SpecialType.System_Delegate && rightType.SpecialType == SpecialType.System_Delegate)
                    {
                        signature = new BinaryOperatorSignature(op | BinaryOperatorKind.Delegate,
                            compilation.GetSpecialType(SpecialType.System_Delegate), compilation.GetSpecialType(SpecialType.System_Delegate),
                            compilation.GetSpecialType(SpecialType.System_Boolean));
                    }
                    else
                    {
                        signature = new BinaryOperatorSignature(op | BinaryOperatorKind.Object, compilation.ObjectType, compilation.ObjectType,
                            compilation.GetSpecialType(SpecialType.System_Boolean));
                    }
                }
                else if (op == BinaryOperatorKind.Addition &&
                    ((leftType.IsStringType() && !rightType.IsPointerType()) || (!leftType.IsPointerType() && rightType.IsStringType())))
                {
                    Assert.False(leftType.IsStringType() && rightType.IsStringType());

                    if (leftType.IsStringType())
                    {
                        signature = new BinaryOperatorSignature(op | BinaryOperatorKind.String, leftType, compilation.ObjectType, leftType);
                    }
                    else
                    {
                        Assert.True(rightType.IsStringType());
                        signature = new BinaryOperatorSignature(op | BinaryOperatorKind.String, compilation.ObjectType, rightType, rightType);
                    }
                }
                else if (op == BinaryOperatorKind.Addition &&
                    (((leftType.IsIntegralType() || leftType.IsCharType()) && rightType.IsPointerType()) ||
                    (leftType.IsPointerType() && (rightType.IsIntegralType() || rightType.IsCharType()))))
                {
                    if (leftType.IsPointerType())
                    {
                        signature = new BinaryOperatorSignature(op | BinaryOperatorKind.Pointer, leftType, symbol1.Parameters[1].Type, leftType);
                        Assert.True(symbol1.Parameters[1].Type.IsIntegralType());
                    }
                    else
                    {
                        signature = new BinaryOperatorSignature(op | BinaryOperatorKind.Pointer, symbol1.Parameters[0].Type, rightType, rightType);
                        Assert.True(symbol1.Parameters[0].Type.IsIntegralType());
                    }
                }
                else if (op == BinaryOperatorKind.Subtraction &&
                    (leftType.IsPointerType() && (rightType.IsIntegralType() || rightType.IsCharType())))
                {
                    signature = new BinaryOperatorSignature(op | BinaryOperatorKind.String, leftType, symbol1.Parameters[1].Type, leftType);
                    Assert.True(symbol1.Parameters[1].Type.IsIntegralType());
                }
                else if (op == BinaryOperatorKind.Subtraction && leftType.IsPointerType() && leftType == rightType)
                {
                    signature = new BinaryOperatorSignature(op | BinaryOperatorKind.Pointer, leftType, rightType, compilation.GetSpecialType(SpecialType.System_Int64));
                }
                else if ((op == BinaryOperatorKind.Addition || op == BinaryOperatorKind.Subtraction) &&
                    leftType.IsEnumType() && (rightType.IsIntegralType() || rightType.IsCharType()) &&
                    (result = OverloadResolution.BinopEasyOut.OpKind(op, leftType.EnumUnderlyingType(), rightType)) != BinaryOperatorKind.Error &&
                    (signature = compilation.builtInOperators.GetSignature(result)).RightType == leftType.EnumUnderlyingType())
                {
                    signature = new BinaryOperatorSignature(signature.Kind | BinaryOperatorKind.EnumAndUnderlying, leftType, signature.RightType, leftType);
                }
                else if ((op == BinaryOperatorKind.Addition || op == BinaryOperatorKind.Subtraction) &&
                    rightType.IsEnumType() && (leftType.IsIntegralType() || leftType.IsCharType()) &&
                    (result = OverloadResolution.BinopEasyOut.OpKind(op, leftType, rightType.EnumUnderlyingType())) != BinaryOperatorKind.Error &&
                    (signature = compilation.builtInOperators.GetSignature(result)).LeftType == rightType.EnumUnderlyingType())
                {
                    signature = new BinaryOperatorSignature(signature.Kind | BinaryOperatorKind.EnumAndUnderlying, signature.LeftType, rightType, rightType);
                }
                else if (op == BinaryOperatorKind.Subtraction && 
                    leftType.IsEnumType() && leftType == rightType)
                {
                    signature = new BinaryOperatorSignature(op | BinaryOperatorKind.Enum, leftType, rightType, leftType.EnumUnderlyingType());
                }
                else if ((op == BinaryOperatorKind.Equal ||
                          op == BinaryOperatorKind.NotEqual ||
                          op == BinaryOperatorKind.LessThan ||
                          op == BinaryOperatorKind.LessThanOrEqual ||
                          op == BinaryOperatorKind.GreaterThan ||
                          op == BinaryOperatorKind.GreaterThanOrEqual) &&
                    leftType.IsEnumType() && leftType == rightType)
                {
                    signature = new BinaryOperatorSignature(op | BinaryOperatorKind.Enum, leftType, rightType, compilation.GetSpecialType(SpecialType.System_Boolean));
                }
                else if ((op == BinaryOperatorKind.Xor ||
                          op == BinaryOperatorKind.And ||
                          op == BinaryOperatorKind.Or) &&
                    leftType.IsEnumType() && leftType == rightType)
                {
                    signature = new BinaryOperatorSignature(op | BinaryOperatorKind.Enum, leftType, rightType, leftType);
                }
                else if ((op == BinaryOperatorKind.Addition || op == BinaryOperatorKind.Subtraction) &&
                    leftType.IsDelegateType() && leftType == rightType)
                {
                    signature = new BinaryOperatorSignature(op | BinaryOperatorKind.Delegate, leftType, leftType, leftType);
                }
                else if ((op == BinaryOperatorKind.Equal ||
                          op == BinaryOperatorKind.NotEqual ||
                          op == BinaryOperatorKind.LessThan ||
                          op == BinaryOperatorKind.LessThanOrEqual ||
                          op == BinaryOperatorKind.GreaterThan ||
                          op == BinaryOperatorKind.GreaterThanOrEqual) &&
                    leftType.IsPointerType() && rightType.IsPointerType())
                {
                    signature = new BinaryOperatorSignature(op | BinaryOperatorKind.Pointer, 
                        compilation.CreatePointerTypeSymbol(compilation.GetSpecialType(SpecialType.System_Void)),
                        compilation.CreatePointerTypeSymbol(compilation.GetSpecialType(SpecialType.System_Void)),
                        compilation.GetSpecialType(SpecialType.System_Boolean));
                }
                else
                {
                    if ((object)symbol1 != null)
                    {
                        Assert.False(symbol1.IsImplicitlyDeclared);
                        Assert.Equal(MethodKind.UserDefinedOperator, symbol1.MethodKind);

                        if (leftType.IsValueType && !leftType.IsPointerType())
                        {
                            if (rightType.IsValueType && !rightType.IsPointerType())
                            {
                                Assert.Same(symbol1, symbol2);
                                Assert.Same(symbol1, symbol3);
                                Assert.Same(symbol1, symbol4);
                                return;
                            }
                            else
                            {
                                Assert.Null(symbol2);
                                Assert.Same(symbol1, symbol3);
                                Assert.Null(symbol4);
                                return;
                            }
                        }
                        else if (rightType.IsValueType && !rightType.IsPointerType())
                        {
                            Assert.Null(symbol2);
                            Assert.Null(symbol3);
                            Assert.Same(symbol1, symbol4);
                            return;
                        }
                        else
                        {
                            Assert.Null(symbol2);
                            Assert.Null(symbol3);
                            Assert.Null(symbol4);
                            return;
                        }
                    }

                    Assert.Null(symbol1);
                    Assert.Null(symbol2);

                    if (!rightType.IsDynamic())
                    {
                        Assert.Null(symbol3);
                    }

                    if (!leftType.IsDynamic())
                    {
                        Assert.Null(symbol4);
                    }
                    return;
                }
            }
            else if ((op == BinaryOperatorKind.Equal || op == BinaryOperatorKind.NotEqual) &&
                leftType != rightType &&
                (!leftType.IsValueType || !rightType.IsValueType ||
                 leftType.SpecialType == SpecialType.System_Boolean || rightType.SpecialType == SpecialType.System_Boolean ||
                 (leftType.SpecialType == SpecialType.System_Decimal && (rightType.SpecialType == SpecialType.System_Double || rightType.SpecialType == SpecialType.System_Single)) ||
                 (rightType.SpecialType == SpecialType.System_Decimal && (leftType.SpecialType == SpecialType.System_Double || leftType.SpecialType == SpecialType.System_Single))) &&
                (!leftType.IsReferenceType || !rightType.IsReferenceType ||
                 !compilation.Conversions.ClassifyConversion(leftType, rightType, ref useSiteDiagnostics).IsReference))
            {
                Assert.Null(symbol1);
                Assert.Null(symbol2);
                Assert.Null(symbol3);
                Assert.Null(symbol4);
                return;
            }
            else
            {
                signature = compilation.builtInOperators.GetSignature(result);
            }

            Assert.NotNull(symbol1);

            string containerName = signature.LeftType.ToTestDisplayString();
            string leftName = containerName;
            string rightName  = signature.RightType.ToTestDisplayString();
            string returnName = signature.ReturnType.ToTestDisplayString();

            if (isDynamic)
            {
                containerName = compilation.DynamicType.ToTestDisplayString();
            }
            else if (op == BinaryOperatorKind.Addition || op == BinaryOperatorKind.Subtraction)
            {
                if (signature.LeftType.IsObjectType() && signature.RightType.IsStringType())
                {
                    containerName = rightName;
                }
                else if ((leftType.IsEnumType() || leftType.IsPointerType()) && (rightType.IsIntegralType() || rightType.IsCharType()))
                {
                    containerName = leftType.ToTestDisplayString();
                    leftName = containerName;
                    returnName = containerName;
                }
                else if ((rightType.IsEnumType() || rightType.IsPointerType()) && (leftType.IsIntegralType() || leftType.IsCharType()))
                {
                    containerName = rightType.ToTestDisplayString();
                    rightName = containerName;
                    returnName = containerName;
                }
            }

            Assert.Equal(isDynamic, signature.ReturnType.IsDynamic());

            string expectedSymbol = String.Format("{4} {0}.{2}({1} left, {3} right)",
                                       containerName,
                                       leftName,
                                       OperatorFacts.BinaryOperatorNameFromOperatorKind(op),
                                       rightName,
                                       returnName);
            string actualSymbol = symbol1.ToTestDisplayString();

            Assert.Equal(expectedSymbol, actualSymbol);

            Assert.Equal(MethodKind.BuiltinOperator, symbol1.MethodKind);
            Assert.True(symbol1.IsImplicitlyDeclared);

            bool isChecked;

            switch (op)
            {
                case BinaryOperatorKind.Multiplication:
                case BinaryOperatorKind.Addition:
                case BinaryOperatorKind.Subtraction:
                case BinaryOperatorKind.Division:
                    isChecked = isDynamic || symbol1.ContainingSymbol.Kind== SymbolKind.PointerType || symbol1.ContainingType.EnumUnderlyingType().SpecialType.IsIntegralType();
                    break;

                default:
                    isChecked = isDynamic;
                    break;
            }

            Assert.Equal(isChecked, symbol1.IsCheckedBuiltin);

            Assert.False(symbol1.IsGenericMethod);
            Assert.False(symbol1.IsExtensionMethod);
            Assert.False(symbol1.IsExtern);
            Assert.False(symbol1.CanBeReferencedByName);
            Assert.Null(symbol1.DeclaringCompilation);
            Assert.Equal(symbol1.Name, symbol1.MetadataName);

            Assert.True(symbol1.ContainingSymbol == symbol1.Parameters[0].Type || symbol1.ContainingSymbol == symbol1.Parameters[1].Type);

            int match = 0;
            if (symbol1.ContainingSymbol == symbol1.ReturnType)
            {
                match++;
            }

            if (symbol1.ContainingSymbol == symbol1.Parameters[0].Type)
            {
                match++;
            }

            if (symbol1.ContainingSymbol == symbol1.Parameters[1].Type)
            {
                match++;
            }

            Assert.True(match >= 2);

            Assert.Equal(0, symbol1.Locations.Length);
            Assert.Null(symbol1.GetDocumentationCommentId());
            Assert.Equal("", symbol1.GetDocumentationCommentXml());

            Assert.True(symbol1.HasSpecialName);
            Assert.True(symbol1.IsStatic);
            Assert.Equal(Accessibility.Public, symbol1.DeclaredAccessibility);
            Assert.False(symbol1.HidesBaseMethodsByName);
            Assert.False(symbol1.IsOverride);
            Assert.False(symbol1.IsVirtual);
            Assert.False(symbol1.IsAbstract);
            Assert.False(symbol1.IsSealed);
            Assert.Equal(2, symbol1.ParameterCount);
            Assert.Equal(0, symbol1.Parameters[0].Ordinal);
            Assert.Equal(1, symbol1.Parameters[1].Ordinal);

            var otherSymbol = (MethodSymbol)semanticModel.GetSymbolInfo(node1).Symbol;
            Assert.Equal(symbol1, otherSymbol);

            if (leftType.IsValueType && !leftType.IsPointerType())
            {
                if (rightType.IsValueType && !rightType.IsPointerType())
                {
                    Assert.Equal(symbol1, symbol2);
                    Assert.Equal(symbol1, symbol3);
                    Assert.Equal(symbol1, symbol4);
                    return;
                }
                else
                {
                    Assert.Null(symbol2);

                    if (rightType.IsDynamic())
                    {
                        Assert.NotEqual(symbol1, symbol3);
                    }
                    else
                    {
                        Assert.Equal(rightType.IsPointerType() ? null : symbol1, symbol3);
                    }

                    Assert.Null(symbol4);
                    return;
                }
            }
            else if (rightType.IsValueType && !rightType.IsPointerType())
            {
                Assert.Null(symbol2);
                Assert.Null(symbol3);

                if (leftType.IsDynamic())
                {
                    Assert.NotEqual(symbol1, symbol4);
                }
                else
                {
                    Assert.Equal(leftType.IsPointerType() ? null : symbol1, symbol4);
                }

                return;
            }

            Assert.Null(symbol2);

            if (rightType.IsDynamic())
            {
                Assert.NotEqual(symbol1, symbol3);
            }
            else
            {
                Assert.Null(symbol3);
            }

            if (leftType.IsDynamic())
            {
                Assert.NotEqual(symbol1, symbol4);
            }
            else
            {
                Assert.Null(symbol4);
            }
        }

        [Fact()]
        public void BinaryIntrinsicSymbols3()
        {
            var source =
@"
class Module1
{
    void Test(object x)
    {
        var z1 = x as string;
        var z2 = x is string;
    }
}";

            var compilation = CreateCompilationWithMscorlib(source, options: TestOptions.ReleaseDll);

            var tree = compilation.SyntaxTrees.Single();
            var semanticModel = compilation.GetSemanticModel(tree);

            var nodes = (from node in tree.GetRoot().DescendantNodes()
                         select node as BinaryExpressionSyntax).
                         Where(node => (object)node != null).ToArray();

            Assert.Equal(2, nodes.Length);

            foreach (var node1 in nodes)
            {
                SymbolInfo info1 = semanticModel.GetSymbolInfo(node1);

                Assert.Null(info1.Symbol);
                Assert.Equal(0, info1.CandidateSymbols.Length);
                Assert.Equal(CandidateReason.None, info1.CandidateReason);
            }
        }

        [Fact()]
        public void CheckedBinaryIntrinsicSymbols()
        {
            var source =
@"
class Module1
{
    void Test(int x, int y)
    {
        var z1 = x + y;
        x += y;
    }
}";

            var compilation = CreateCompilationWithMscorlib(source, options: TestOptions.ReleaseDll.WithOverflowChecks(false));

            var tree = compilation.SyntaxTrees.Single();
            var semanticModel = compilation.GetSemanticModel(tree);

            var nodes = tree.GetRoot().DescendantNodes().Where(node => node is BinaryExpressionSyntax || node is AssignmentExpressionSyntax).ToArray();

            Assert.Equal(2, nodes.Length);

            var symbols1 = (from node1 in nodes select (MethodSymbol)semanticModel.GetSymbolInfo(node1).Symbol).ToArray();
            foreach (var symbol1 in symbols1)
            {
                Assert.False(symbol1.IsCheckedBuiltin);
            }

            compilation = compilation.WithOptions(TestOptions.ReleaseDll.WithOverflowChecks(true));
            semanticModel = compilation.GetSemanticModel(tree);

            var symbols2 = (from node2 in nodes select (MethodSymbol)semanticModel.GetSymbolInfo(node2).Symbol).ToArray();
            foreach (var symbol2 in symbols2)
            {
                Assert.True(symbol2.IsCheckedBuiltin);
            }

            for (int i = 0; i < symbols1.Length; i++)
            {
                Assert.NotEqual(symbols1[i], symbols2[i]);
            }
        }

        [Fact()]
        public void DynamicBinaryIntrinsicSymbols()
        {
            var source =
@"
class Module1
{
    void Test(dynamic x)
    {
        var z1 = x == null;
        var z2 = null == x;
    }
}";

            var compilation = CreateCompilationWithMscorlib(source, options: TestOptions.ReleaseDll.WithOverflowChecks(false));

            var tree = compilation.SyntaxTrees.Single();
            var semanticModel = compilation.GetSemanticModel(tree);

            var nodes = (from node in tree.GetRoot().DescendantNodes()
                         select node as BinaryExpressionSyntax).
                         Where(node => (object)node != null).ToArray();

            Assert.Equal(2, nodes.Length);

            var symbols1 = (from node1 in nodes select (MethodSymbol)semanticModel.GetSymbolInfo(node1).Symbol).ToArray();
            foreach (var symbol1 in symbols1)
            {
                Assert.False(symbol1.IsCheckedBuiltin);
                Assert.True(((TypeSymbol)symbol1.ContainingSymbol).IsDynamic());
                Assert.Null(symbol1.ContainingType);
            }

            compilation = compilation.WithOptions(TestOptions.ReleaseDll.WithOverflowChecks(true));
            semanticModel = compilation.GetSemanticModel(tree);

            var symbols2 = (from node2 in nodes select (MethodSymbol)semanticModel.GetSymbolInfo(node2).Symbol).ToArray();
            foreach (var symbol2 in symbols2)
            {
                Assert.True(symbol2.IsCheckedBuiltin);
                Assert.True(((TypeSymbol)symbol2.ContainingSymbol).IsDynamic());
                Assert.Null(symbol2.ContainingType);
            }

            for (int i = 0; i < symbols1.Length; i++)
            {
                Assert.NotEqual(symbols1[i], symbols2[i]);
            }
        }

        [Fact(), WorkItem(721565, "DevDiv")]
        public void Bug721565()
        {
            var source =
@"
class Module1
{
    void Test(TestStr? x, int? y, TestStr? x1, int? y1)
    {
        var z1 = (x == null);
        var z2 = (x != null);
        var z3 = (null == x);
        var z4 = (null != x);
        var z5 = (y == null);
        var z6 = (y != null);
        var z7 = (null == y);
        var z8 = (null != y);

        var z9 = (y == y1);
        var z10 = (y != y1);
        var z11 = (x == x1);
        var z12 = (x != x1);
    }
}

struct TestStr
{}
";

            var compilation = CreateCompilationWithMscorlib(source, options: TestOptions.ReleaseDll);

            compilation.VerifyDiagnostics(
                // (17,20): error CS0019: Operator '==' cannot be applied to operands of type 'TestStr?' and 'TestStr?'
                //         var z11 = (x == x1);
    Diagnostic(ErrorCode.ERR_BadBinaryOps, "x == x1").WithArguments("==", "TestStr?", "TestStr?"),
                // (18,20): error CS0019: Operator '!=' cannot be applied to operands of type 'TestStr?' and 'TestStr?'
                //         var z12 = (x != x1);
    Diagnostic(ErrorCode.ERR_BadBinaryOps, "x != x1").WithArguments("!=", "TestStr?", "TestStr?")
                );

            var tree = compilation.SyntaxTrees.Single();
            var semanticModel = compilation.GetSemanticModel(tree);

            var nodes = (from node in tree.GetRoot().DescendantNodes()
                         select node as BinaryExpressionSyntax).
                         Where(node => (object)node != null).ToArray();

            Assert.Equal(12, nodes.Length);

            for (int i = 0; i < 12; i++)
            {
                SymbolInfo info1 = semanticModel.GetSymbolInfo(nodes[i]);

                switch (i)
                {
                    case 0:
                    case 2:
                    case 4:
                    case 6:
                        Assert.Equal("System.Boolean System.Object.op_Equality(System.Object left, System.Object right)", info1.Symbol.ToTestDisplayString());
                        break;
                    case 1:
                    case 3:
                    case 5:
                    case 7:
                        Assert.Equal("System.Boolean System.Object.op_Inequality(System.Object left, System.Object right)", info1.Symbol.ToTestDisplayString());
                        break;
                    case 8:
                        Assert.Equal("System.Boolean System.Int32.op_Equality(System.Int32 left, System.Int32 right)", info1.Symbol.ToTestDisplayString());
                        break;
                    case 9:
                        Assert.Equal("System.Boolean System.Int32.op_Inequality(System.Int32 left, System.Int32 right)", info1.Symbol.ToTestDisplayString());
                        break;
                    case 10:
                    case 11:
                        Assert.Null(info1.Symbol);
                        break;
                    default:
                        throw Roslyn.Utilities.ExceptionUtilities.UnexpectedValue(i);
                }
            }
        }

        [Fact]
        public void IntrinsicBinaryOperatorSignature_EqualsAndGetHashCode()
        {
            var source =
@"class C
{
    static object F(int i)
    {
        return i += 1;
    }
}";
            var compilation = CreateCompilationWithMscorlib(source, options: TestOptions.ReleaseDll);
            compilation.VerifyDiagnostics();

            var tree = compilation.SyntaxTrees[0];
            var methodDecl = tree.GetCompilationUnitRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
            var methodBody = methodDecl.Body;
            var model = (CSharpSemanticModel)compilation.GetSemanticModel(tree);
            var binder = model.GetEnclosingBinder(methodBody.SpanStart);
            var diagnostics = DiagnosticBag.GetInstance();
            var block = binder.BindBlock(methodBody, diagnostics);
            diagnostics.Free();

            // Rewriter should use Equals.
            var rewriter = new EmptyRewriter();
            var node = rewriter.Visit(block);
            Assert.Same(node, block);

            var visitor = new FindCompoundAssignmentWalker();
            visitor.Visit(block);
            var op = visitor.FirstNode.Operator;
            Assert.Null(op.Method);
            // Equals and GetHashCode should support null Method.
            Assert.Equal(op, new BinaryOperatorSignature(op.Kind, op.LeftType, op.RightType, op.ReturnType, op.Method));
            op.GetHashCode();
        }

        private sealed class EmptyRewriter : BoundTreeRewriter
        {
        }

        private sealed class FindCompoundAssignmentWalker : BoundTreeWalker
        {
            internal BoundCompoundAssignmentOperator FirstNode;

            public override BoundNode VisitCompoundAssignmentOperator(BoundCompoundAssignmentOperator node)
            {
                if (FirstNode == null)
                {
                    FirstNode = node;
                }
                return base.VisitCompoundAssignmentOperator(node);
            }
        }
    }
}
