﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.CodeGen
{
    public class CodeGenAsyncSpillTests : EmitMetadataTestBase
    {
        private static readonly MetadataReference[] AsyncRefs = new[] { MscorlibRef_v4_0_30316_17626, SystemRef_v4_0_30319_17929, SystemCoreRef_v4_0_30319_17929 };

        public CodeGenAsyncSpillTests()
        {
            SynchronizationContext.SetSynchronizationContext(null);
        }

        private CompilationVerifier CompileAndVerify(string source, string expectedOutput = null, IEnumerable<MetadataReference> references = null, TestEmitters emitOptions = TestEmitters.All, CSharpCompilationOptions options = null)
        {
            references = (references != null) ? references.Concat(AsyncRefs) : AsyncRefs;
            return base.CompileAndVerify(source, expectedOutput: expectedOutput, additionalRefs: references, options: options, emitOptions: emitOptions);
        }

        [Fact]
        public void AsyncWithTernary()
        {
            var source = @"
using System;
using System.Threading.Tasks;

class Test
{
    public static Task<T> F<T>(T x)
    {
        Console.WriteLine(""F("" + x + "")"");
        return Task.Factory.StartNew(() => { return x; });
    }

    public static async Task<int> G(bool b1, bool b2)
    {
        int c = 0;
        c = c + (b1 ? 1 : await F(2));
        c = c + (b2 ? await F(4) : 8);
        return await F(c);
    }

    public static int H(bool b1, bool b2)
    {
        Task<int> t = G(b1, b2);
        t.Wait(1000 * 60);
        return t.Result;
    }

    public static void Main()
    {
        Console.WriteLine(H(false, false));
        Console.WriteLine(H(false, true));
        Console.WriteLine(H(true, false));
        Console.WriteLine(H(true, true));
    }
}";
            var expected = @"
F(2)
F(10)
10
F(2)
F(4)
F(6)
6
F(9)
9
F(4)
F(5)
5
";
            CompileAndVerify(source, expectedOutput: expected);
        }

        [Fact]
        public void AsyncWithAnd()
        {
            var source = @"
using System;
using System.Threading.Tasks;

class Test
{
    public static Task<T> F<T>(T x)
    {
        Console.WriteLine(""F("" + x + "")"");
        return Task.Factory.StartNew(() => { return x; });
    }

    public static async Task<int> G(bool b1, bool b2)
    {
        bool x1 = b1 && await F(true);
        bool x2 = b1 && await F(false);
        bool x3 = b2 && await F(true);
        bool x4 = b2 && await F(false);
        int c = 0;
        if (x1) c += 1;
        if (x2) c += 2;
        if (x3) c += 4;
        if (x4) c += 8;
        return await F(c);
    }

    public static int H(bool b1, bool b2)
    {
        Task<int> t = G(b1, b2);
        t.Wait(1000 * 60);
        return t.Result;
    }

    public static void Main()
    {
        Console.WriteLine(H(false, true));
    }
}";
            var expected = @"
F(True)
F(False)
F(4)
4
";
            CompileAndVerify(source, expectedOutput: expected);
        }

        [Fact]
        public void AsyncWithOr()
        {
            var source = @"
using System;
using System.Threading.Tasks;

class Test
{
    public static Task<T> F<T>(T x)
    {
        Console.WriteLine(""F("" + x + "")"");
        return Task.Factory.StartNew(() => { return x; });
    }

    public static async Task<int> G(bool b1, bool b2)
    {
        bool x1 = b1 || await F(true);
        bool x2 = b1 || await F(false);
        bool x3 = b2 || await F(true);
        bool x4 = b2 || await F(false);
        int c = 0;
        if (x1) c += 1;
        if (x2) c += 2;
        if (x3) c += 4;
        if (x4) c += 8;
        return await F(c);
    }

    public static int H(bool b1, bool b2)
    {
        Task<int> t = G(b1, b2);
        t.Wait(1000 * 60);
        return t.Result;
    }

    public static void Main()
    {
        Console.WriteLine(H(false, true));
    }
}";
            var expected = @"
F(True)
F(False)
F(13)
13
";
            CompileAndVerify(source, expectedOutput: expected);
        }

        [Fact]
        public void AsyncWithCoalesce()
        {
            var source = @"
using System;
using System.Threading.Tasks;

class Test
{
    public static Task<string> F(string x)
    {
        Console.WriteLine(""F("" + (x ?? ""null"") + "")"");
        return Task.Factory.StartNew(() => { return x; });
    }

    public static async Task<string> G(string s1, string s2)
    {
        var result = await F(s1) ?? await F(s2);
        Console.WriteLine("" "" + (result ?? ""null""));
        return result;
    }

    public static string H(string s1, string s2)
    {
        Task<string> t = G(s1, s2);
        t.Wait(1000 * 60);
        return t.Result;
    }

    public static void Main()
    {
        H(null, null);
        H(null, ""a"");
        H(""b"", null);
        H(""c"", ""d"");
    }
}";
            var expected = @"
F(null)
F(null)
 null
F(null)
F(a)
 a
F(b)
 b
F(c)
 c
";
            CompileAndVerify(source, expectedOutput: expected);
        }

        [Fact]
        public void AwaitInExpr()
        {
            var source = @"
using System;
using System.Threading.Tasks;

class Test
{
    public static async Task<int> F()
    {
        return await Task.Factory.StartNew(() => 21);
    }

    public static async Task<int> G()
    {
        int c = 0;
        c = (await F()) + 21;
        return c;
    }

    public static void Main()
    {
        Task<int> t = G();
        t.Wait(1000 * 60);
        Console.WriteLine(t.Result);
    }
}";
            var expected = @"
42
";
            CompileAndVerify(source, expectedOutput: expected);
        }

        [Fact]
        public void SpillNestedUnary()
        {
            var source = @"
using System;
using System.Threading.Tasks;

class Test
{
    public static async Task<int> F()
    {
        return 1;
    }

    public static async Task<int> G1()
    {
        return -(await F());
    }

    public static async Task<int> G2()
    {
        return -(-(await F()));
    }

    public static async Task<int> G3()
    {
        return -(-(-(await F())));
    }

    public static void WaitAndPrint(Task<int> t)
    {
        t.Wait();
        Console.WriteLine(t.Result);
    }

    public static void Main()
    {
        WaitAndPrint(G1());
        WaitAndPrint(G2());
        WaitAndPrint(G3());
    }
}";
            var expected = @"
-1
1
-1
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void AsyncWithParamsAndLocals_DoubleAwait_Spilling()
        {
            var source = @"
using System;
using System.Threading.Tasks;

class Test
{
    public static async Task<int> F(int x)
    {
        return await Task.Factory.StartNew(() => { return x; });
    }

    public static async Task<int> G(int x)
    {
        int c = 0;
        c = (await F(x)) + c;
        c = (await F(x)) + c;
        return c;
    }

    public static void Main()
    {
        Task<int> t = G(21);
        t.Wait(1000 * 60);
        Console.WriteLine(t.Result);
    }
}";
            var expected = @"
42
";
            // When the local 'c' gets hoisted, the statement:
            //   c = (await F(x)) + c;
            // Gets rewritten to:
            //   this.c_field = (await F(x)) + this.c_field;
            //
            // The code-gen for the assignment is something like this:
            //   ldarg0  // load the 'this' reference to the stack
            //   <emitted await expression>
            //   stfld
            //
            // What we really want is to evaluate any parts of the lvalue that have side-effects (which is this case is
            // nothing), and then defer loading the address for the field reference until after the await expression:
            //   <emitted await expression>
            //   <store to tmp>
            //   ldarg0
            //   <load tmp>
            //   stfld
            //
            // So this case actually requires stack spilling, which is not yet implemented. This has the unfortunate
            // consequence of preventing await expressions from being assigned to hoisted locals.
            //
            CompileAndVerify(source, expectedOutput: expected);
        }

        [Fact]
        public void SpillCall()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

class Test
{
    public static void Printer(int a, int b, int c, int d, int e)
    {
        foreach (var x in new List<int>() { a, b, c, d, e })
        {
            Console.WriteLine(x);
        }
    }

    public static int Get(int x)
    {
        Console.WriteLine(""> "" + x);
        return x;
    }

    public static async Task<int> F(int x)
    {
        return await Task.Factory.StartNew(() => x);
    }

    public static async Task G()
    {
        Printer(Get(111), Get(222), Get(333), await F(Get(444)), Get(555));
    }

    public static void Main()
    {
        Task t = G();
        t.Wait();
    }
}";
            var expected = @"
> 111
> 222
> 333
> 444
> 555
111
222
333
444
555
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillCall2()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

class Test
{
    public static void Printer(int a, int b, int c, int d, int e)
    {
        foreach (var x in new List<int>() { a, b, c, d, e })
        {
            Console.WriteLine(x);
        }
    }

    public static int Get(int x)
    {
        Console.WriteLine(""> "" + x);
        return x;
    }

    public static async Task<int> F(int x)
    {
        return await Task.Factory.StartNew(() => x);
    }

    public static async Task G()
    {
        Printer(Get(111), await F(Get(222)), Get(333), await F(Get(444)), Get(555));
    }

    public static void Main()
    {
        Task t = G();
        t.Wait();
    }
}";
            var expected = @"
> 111
> 222
> 333
> 444
> 555
111
222
333
444
555
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillCall3()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

class Test
{
    public static void Printer(int a, int b, int c, int d, int e, int f)
    {
        foreach (var x in new List<int>(){a, b, c, d, e, f})
        {
            Console.WriteLine(x);
        }
    }

    public static async Task<int> F(int x)
    {
        return await Task.Factory.StartNew(() => x);
    }

    public static async Task G()
    {
        Printer(1, await F(2), 3, await F(await F(await F(await F(4)))), await F(5), 6);
    }

    public static void Main()
    {
        Task t = G();
        t.Wait();
    }
}";
            var expected = @"
1
2
3
4
5
6
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillCall4()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

class Test
{
    public static void Printer(int a, int b)
    {
        foreach (var x in new List<int>(){a, b})
        {
            Console.WriteLine(x);
        }
    }

    public static async Task<int> F(int x)
    {
        return await Task.Factory.StartNew(() => x);
    }

    public static async Task G()
    {
        Printer(1, await F(await F(2)));
    }

    public static void Main()
    {
        Task t = G();
        t.Wait();
    }
}";
            var expected = @"
1
2
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillSequences1()
        {
            var source = @"
using System.Threading.Tasks;

public class Test
{
    public static int H(int a, int b, int c)
    {
        return a;
    }

    public static Task<int> G()
    {
        return null;
    }

    public static async Task<int> F(int[] array)
    {
        H(array[1] += 2, array[3] += await G(), 4);
        return 1;
    }
}
";
            var v = CompileAndVerify(source, options: TestOptions.DebugDll);

            v.VerifyIL("Test.<F>d__1.System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext", @"
{
  // Code size      297 (0x129)
  .maxstack  4
  .locals init (int V_0,
                int V_1,
                int& V_2,
                int V_3,
                int V_4,
                int V_5,
                int& V_6,
                System.Runtime.CompilerServices.TaskAwaiter<int> V_7,
                Test.<F>d__1 V_8,
                System.Exception V_9)
 ~IL_0000:  ldarg.0
  IL_0001:  ldfld      ""int Test.<F>d__1.<>1__state""
  IL_0006:  stloc.0
  .try
  {
   ~IL_0007:  ldloc.0
    IL_0008:  brfalse.s  IL_000c
    IL_000a:  br.s       IL_0011
    IL_000c:  br         IL_0096
   -IL_0011:  nop
   -IL_0012:  ldarg.0
    IL_0013:  ldfld      ""int[] Test.<F>d__1.array""
    IL_0018:  ldc.i4.1
    IL_0019:  ldelema    ""int""
    IL_001e:  stloc.2
    IL_001f:  ldarg.0
    IL_0020:  ldloc.2
    IL_0021:  ldloc.2
    IL_0022:  ldind.i4
    IL_0023:  ldc.i4.2
    IL_0024:  add
    IL_0025:  dup
    IL_0026:  stloc.s    V_5
    IL_0028:  stind.i4
    IL_0029:  ldloc.s    V_5
    IL_002b:  stfld      ""int Test.<F>d__1.<>7__wrap1""
    IL_0030:  ldarg.0
    IL_0031:  ldarg.0
    IL_0032:  ldfld      ""int[] Test.<F>d__1.array""
    IL_0037:  stfld      ""int[] Test.<F>d__1.<>7__wrap3""
    IL_003c:  ldarg.0
    IL_003d:  ldfld      ""int[] Test.<F>d__1.<>7__wrap3""
    IL_0042:  ldc.i4.3
    IL_0043:  ldelema    ""int""
    IL_0048:  stloc.s    V_6
    IL_004a:  ldarg.0
    IL_004b:  ldarg.0
    IL_004c:  ldfld      ""int[] Test.<F>d__1.<>7__wrap3""
    IL_0051:  ldc.i4.3
    IL_0052:  ldelem.i4
    IL_0053:  stfld      ""int Test.<F>d__1.<>7__wrap2""
    IL_0058:  call       ""System.Threading.Tasks.Task<int> Test.G()""
    IL_005d:  callvirt   ""System.Runtime.CompilerServices.TaskAwaiter<int> System.Threading.Tasks.Task<int>.GetAwaiter()""
    IL_0062:  stloc.s    V_7
    IL_0064:  ldloca.s   V_7
    IL_0066:  call       ""bool System.Runtime.CompilerServices.TaskAwaiter<int>.IsCompleted.get""
    IL_006b:  brtrue.s   IL_00b3
    IL_006d:  ldarg.0
    IL_006e:  ldc.i4.0
    IL_006f:  dup
    IL_0070:  stloc.0
    IL_0071:  stfld      ""int Test.<F>d__1.<>1__state""
    IL_0076:  ldarg.0
    IL_0077:  ldloc.s    V_7
    IL_0079:  stfld      ""System.Runtime.CompilerServices.TaskAwaiter<int> Test.<F>d__1.<>u__$awaiter0""
    IL_007e:  ldarg.0
    IL_007f:  stloc.s    V_8
    IL_0081:  ldarg.0
    IL_0082:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder<int> Test.<F>d__1.<>t__builder""
    IL_0087:  ldloca.s   V_7
    IL_0089:  ldloca.s   V_8
    IL_008b:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<int>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter<int>, Test.<F>d__1>(ref System.Runtime.CompilerServices.TaskAwaiter<int>, ref Test.<F>d__1)""
    IL_0090:  nop
    IL_0091:  leave      IL_0128
    IL_0096:  ldarg.0
    IL_0097:  ldfld      ""System.Runtime.CompilerServices.TaskAwaiter<int> Test.<F>d__1.<>u__$awaiter0""
    IL_009c:  stloc.s    V_7
    IL_009e:  ldarg.0
    IL_009f:  ldflda     ""System.Runtime.CompilerServices.TaskAwaiter<int> Test.<F>d__1.<>u__$awaiter0""
    IL_00a4:  initobj    ""System.Runtime.CompilerServices.TaskAwaiter<int>""
    IL_00aa:  ldarg.0
    IL_00ab:  ldc.i4.m1
    IL_00ac:  dup
    IL_00ad:  stloc.0
    IL_00ae:  stfld      ""int Test.<F>d__1.<>1__state""
    IL_00b3:  ldloca.s   V_7
    IL_00b5:  call       ""int System.Runtime.CompilerServices.TaskAwaiter<int>.GetResult()""
    IL_00ba:  stloc.s    V_5
    IL_00bc:  ldloca.s   V_7
    IL_00be:  initobj    ""System.Runtime.CompilerServices.TaskAwaiter<int>""
    IL_00c4:  ldloc.s    V_5
    IL_00c6:  stloc.3
    IL_00c7:  ldarg.0
    IL_00c8:  ldfld      ""int[] Test.<F>d__1.<>7__wrap3""
    IL_00cd:  ldc.i4.3
    IL_00ce:  ldarg.0
    IL_00cf:  ldfld      ""int Test.<F>d__1.<>7__wrap2""
    IL_00d4:  ldloc.3
    IL_00d5:  add
    IL_00d6:  dup
    IL_00d7:  stloc.s    V_5
    IL_00d9:  stelem.i4
    IL_00da:  ldloc.s    V_5
    IL_00dc:  stloc.s    V_4
    IL_00de:  ldarg.0
    IL_00df:  ldfld      ""int Test.<F>d__1.<>7__wrap1""
    IL_00e4:  ldloc.s    V_4
    IL_00e6:  ldc.i4.4
    IL_00e7:  call       ""int Test.H(int, int, int)""
    IL_00ec:  pop
    IL_00ed:  ldarg.0
    IL_00ee:  ldnull
    IL_00ef:  stfld      ""int[] Test.<F>d__1.<>7__wrap3""
   -IL_00f4:  ldc.i4.1
    IL_00f5:  stloc.1
    IL_00f6:  leave.s    IL_0113
  }
  catch System.Exception
  {
   ~IL_00f8:  stloc.s    V_9
    IL_00fa:  nop
    IL_00fb:  ldarg.0
    IL_00fc:  ldc.i4.s   -2
    IL_00fe:  stfld      ""int Test.<F>d__1.<>1__state""
    IL_0103:  ldarg.0
    IL_0104:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder<int> Test.<F>d__1.<>t__builder""
    IL_0109:  ldloc.s    V_9
    IL_010b:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<int>.SetException(System.Exception)""
    IL_0110:  nop
    IL_0111:  leave.s    IL_0128
  }
 -IL_0113:  ldarg.0
  IL_0114:  ldc.i4.s   -2
  IL_0116:  stfld      ""int Test.<F>d__1.<>1__state""
 ~IL_011b:  ldarg.0
  IL_011c:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder<int> Test.<F>d__1.<>t__builder""
  IL_0121:  ldloc.1
  IL_0122:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<int>.SetResult(int)""
  IL_0127:  nop
  IL_0128:  ret
}", sequencePoints: "Test+<F>d__1.MoveNext");
        }

        [Fact]
        public void SpillSequencesInConditionalExpression1()
        {
            var source = @"
using System.Threading.Tasks;

public class Test
{
    public static int H(int a, int b, int c)
    {
        return a;
    }

    public static Task<int> G()
    {
        return null;
    }

    public static async Task<int> F(int[] array)
    {
        H(0, (1 == await G()) ? array[3] += await G() : 1, 4);
        return 1;
    }
}
";
            CompileAndVerify(source, options: TestOptions.DebugDll);
        }

        [Fact]
        public void SpillSequencesInNullCoalescingOperator1()
        {
            var source = @"
using System.Threading.Tasks;

public class C
{
    public static int H(int a, object b, int c)
    {
        return a;
    }

    public static object O(int a)
    {
        return null;
    }

    public static Task<int> G()
    {
        return null;
    }

    public static async Task<int> F(int[] array)
    {
        H(0, O(array[0] += await G()) ?? (array[1] += await G()), 4);
        return 1;
    }
}
";
            CompileAndVerify(source, additionalRefs: AsyncRefs, options: TestOptions.ReleaseDll.WithMetadataImportOptions(MetadataImportOptions.All), symbolValidator: module =>
            {
                AssertEx.Equal(new[]
                {
                    "<>1__state",
                    "<>t__builder",
                    "array",
                    "<>7__wrap1",
                    "<>7__wrap2",
                    "<>u__$awaiter0",
                    "<>7__wrap3",
                    "<>7__wrap4",
                }, module.GetFieldNames("C.<F>d__1"));
            });
#if TODO
            CompileAndVerify(source, additionalRefs: AsyncRefs, verify:false, options: TestOptions.DebugDll.WithMetadataImportOptions(MetadataImportOptions.All), symbolValidator: module =>
            {
                AssertEx.Equal(new[]
                {
                    "<>1__state",
                    "<>t__builder",
                    "array",
                    "<>s__530$1$2",
                    "<>s__530$1$7",
                    "<>s__531$1$1",
                    "<>u__$awaiter0",
                    "<>s__531$1$2",
                }, module.GetFieldNames("C.<F>d__1"));
            });
#endif
        }

        [Fact]
        public void SpillSequencesInLogicalBinaryOperator1()
        {
            var source = @"
using System.Threading.Tasks;

public class Test
{
    public static int H(int a, bool b, int c)
    {
        return a;
    }

    public static bool B(int a)
    {
        return true;
    }

    public static Task<int> G()
    {
        return null;
    }

    public static async Task<int> F(int[] array)
    {
        H(0, B(array[0] += await G()) || B(array[1] += await G()), 4);
        return 1;
    }
}
";
            CompileAndVerify(source, options: TestOptions.DebugDll);
        }

        [Fact]
        public void SpillArray01()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    public async void Run<T>(T t) where T : struct
    {
        int tests = 0;
        try
        {
            tests++;
            int[] arr = new int[await GetVal(4)];
            if (arr.Length == 4)
                Driver.Count++;

            //multidimensional
            tests++;
            decimal[,] arr2 = new decimal[await GetVal(4), await GetVal(4)];
            if (arr2.Rank == 2 && arr2.Length == 16)
                Driver.Count++;

            arr2 = new decimal[4, await GetVal(4)];
            if (arr2.Rank == 2 && arr2.Length == 16)
                Driver.Count++;

            tests++;
            arr2 = new decimal[await GetVal(4), 4];
            if (arr2.Rank == 2 && arr2.Length == 16)
                Driver.Count++;


            //jagged array
            tests++;
            decimal?[][] arr3 = new decimal?[await GetVal(4)][];
            if (arr3.Rank == 2 && arr3.Length == 4)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run(6);

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            var expected = @"
0
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArray02_1()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    public async void Run<T>(T t) where T : struct
    {
        int tests = 0;
        try
        {
            tests++;
            int[] arr = new int[await GetVal(4)];
            if (arr.Length == 4)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run(6);

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            var expected = @"
0
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArray02_2()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    public async void Run<T>(T t) where T : struct
    {
        int tests = 0;
        try
        {
            int[] arr = new int[4];    
        
            tests++;
            arr[0] = await GetVal(4);
            if (arr[0] == 4)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run(6);

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            var expected = @"
0
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArray02_3()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    public async void Run<T>(T t) where T : struct
    {
        int tests = 0;
        try
        {
            int[] arr = new int[4];  
            arr[0] = 4;  
            
            tests++;
            arr[0] += await GetVal(4);
            if (arr[0] == 8)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run(6);

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            var expected = @"
0
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArray02_4()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    public async void Run<T>(T t) where T : struct
    {
        int tests = 0;
        try
        {
            int[] arr = new int[] { 8, 0, 0, 0 };

            tests++;
            arr[1] += await (GetVal(arr[0]));
            if (arr[1] == 8)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run(6);

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            var expected = @"
0
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArray02_5()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    public async void Run<T>(T t) where T : struct
    {
        int tests = 0;
        try
        {
            int[] arr = new int[] { 8, 8, 0, 0 };
            
            tests++;
            arr[1] += await (GetVal(arr[await GetVal(0)]));
            if (arr[1] == 16)
                Driver.Count++;

            tests++;
            arr[await GetVal(2)]++;
            if (arr[2] == 1)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run(6);

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            var expected = @"
0
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArray02_6()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    public async void Run<T>(T t) where T : struct
    {
        int tests = 0;
        try
        {
            int[] arr = new int[4];

            tests++;
            arr[await GetVal(2)]++;
            if (arr[2] == 1)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run(6);

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            var expected = @"
0
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArray03()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    public async void Run<T>(T t) where T : struct
    {
        int tests = 0;
        try
        {
            int[,] arr = new int[await GetVal(4), await GetVal(4)];

            tests++;
            arr[0, 0] = await GetVal(4);
            if (arr[0, await (GetVal(0))] == 4)
                Driver.Count++;

            tests++;
            arr[0, 0] += await GetVal(4);
            if (arr[0, 0] == 8)
                Driver.Count++;

            tests++;
            arr[1, 1] += await (GetVal(arr[0, 0]));
            if (arr[1, 1] == 8)
                Driver.Count++;

            tests++;
            arr[1, 1] += await (GetVal(arr[0, await GetVal(0)]));
            if (arr[1, 1] == 16)
                Driver.Count++;

            tests++;
            arr[2, await GetVal(2)]++;
            if (arr[2, 2] == 1)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run(6);

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            var expected = @"
0
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArray04()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using System;

struct MyStruct<T>
{
    T t { get; set; }
    public T this[T index]
    {
        get
        {
            return t;
        }
        set
        {
            t = value;
        }
    }
}

struct TestCase
{
    public async void Run()
    {
        try
        {
            MyStruct<int> ms = new MyStruct<int>();
            var x = ms[index: await Foo()];
        }
        finally
        {
            Driver.CompletedSignal.Set();
        }
    }
    public async Task<int> Foo()
    {
        await Task.Delay(1);
        return 1;
    }
}

class Driver
{
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run();
        CompletedSignal.WaitOne();
    }
}";
            CompileAndVerify(source, "");
        }

        [Fact]
        public void SpillArrayAssign()
        {
            var source = @"
using System;
using System.Threading.Tasks;

class TestCase
{
    static int[] arr = new int[4];

    static async Task Run()
    {
        arr[0] = await Task.Factory.StartNew(() => 42);
    }

    static void Main()
    {
        Task task = Run();
        task.Wait();
        Console.WriteLine(arr[0]);
    }
}";
            var expected = @"
42
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArrayLocal()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    public async void Run<T>(T t) where T : struct
    {
        int[] arr = new int[2] { -1, 42 };

        int tests = 0;
        try
        {
            tests++;
            int t1 = arr[await GetVal(1)];
            if (t1 == 42)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run(6);

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            var expected = @"
0
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArrayCompoundAssignmentLValue()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Driver
{
    static int[] arr;

    static async Task Run()
    {
        arr = new int[1];
        arr[0] += await Task.Factory.StartNew(() => 42);
    }

    static void Main()
    {
        Run().Wait();
        Console.WriteLine(arr[0]);
    }
}";
            var expected = @"
42
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArrayCompoundAssignmentLValueAwait()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Driver
{
    static int[] arr;

    static async Task Run()
    {
        arr = new int[1];
        arr[await Task.Factory.StartNew(() => 0)] += await Task.Factory.StartNew(() => 42);
    }

    static void Main()
    {
        Run().Wait();
        Console.WriteLine(arr[0]);
    }
}";
            var expected = @"
42
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArrayCompoundAssignmentLValueAwait2()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

struct S1
{
    public int x;
}

struct S2
{
    public S1 s1;
}

class Driver
{
    static async Task<int> Run()
    {
        var arr = new S2[1];
        arr[await Task.Factory.StartNew(() => 0)].s1.x += await Task.Factory.StartNew(() => 42);
        return arr[await Task.Factory.StartNew(() => 0)].s1.x;
    }

    static void Main()
    {
        var t = Run();
        t.Wait();
        Console.WriteLine(t.Result);
    }
}";
            var expected = @"
42
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void DoubleSpillArrayCompoundAssignment()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

struct S1
{
    public int x;
}

struct S2
{
    public S1 s1;
}

class Driver
{
    static async Task<int> Run()
    {
        var arr = new S2[1];
        arr[await Task.Factory.StartNew(() => 0)].s1.x += (arr[await Task.Factory.StartNew(() => 0)].s1.x += await Task.Factory.StartNew(() => 42));
        return arr[await Task.Factory.StartNew(() => 0)].s1.x;
    }

    static void Main()
    {
        var t = Run();
        t.Wait();
        Console.WriteLine(t.Result);
    }
}";
            var expected = @"
42
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArrayInitializers1()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    public async void Run()
    {
        int tests = 0;
        try
        {
            //jagged array
            tests++;
            int[][] arr1 = new[]
                {
                    new []{await GetVal(2),await GetVal(3)},
                    new []{4,await GetVal(5),await GetVal(6)}
                };
            if (arr1[0][1] == 3 && arr1[1][1] == 5 && arr1[1][2] == 6)
                Driver.Count++;

            tests++;
            int[][] arr2 = new[]
                {
                    new []{await GetVal(2),await GetVal(3)},
                    await Foo()
                };
            if (arr2[0][1] == 3 && arr2[1][1] == 2)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }

    public async Task<int[]> Foo()
    {
        await Task.Delay(1);
        return new int[] { 1, 2, 3 };
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run();

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            var expected = @"
0
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArrayInitializers2()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    public async void Run()
    {
        int tests = 0;
        try
        {
            //jagged array
            tests++;
            int[,] arr1 = 
                {
                    {await GetVal(2),await GetVal(3)},
                    {await GetVal(5),await GetVal(6)}
                };
            if (arr1[0, 1] == 3 && arr1[1, 0] == 5 && arr1[1, 1] == 6)
                Driver.Count++;

            tests++;
            int[,] arr2 = 
                {
                    {await GetVal(2),3},
                    {4,await GetVal(5)}
                };
            if (arr2[0, 1] == 3 && arr2[1, 1] == 5)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run();

        CompletedSignal.WaitOne();
        Console.WriteLine(Driver.Result);
    }
}";
            var expected = @"
0
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArrayInitializers3()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    public async void Run()
    {
        int tests = 0;
        try
        {
            //jagged array
            tests++;
            int[][] arr1 = new[]
                {
                    new []{await GetVal(2),await Task.Run<int>(async()=>{await Task.Delay(1);return 3;})},
                    new []{await GetVal(5),4,await Task.Run<int>(async()=>{await Task.Delay(1);return 6;})}
                };
            if (arr1[0][1] == 3 && arr1[1][1] == 4 && arr1[1][2] == 6)
                Driver.Count++;

            tests++;
            dynamic arr2 = new[]
                {
                    new []{await GetVal(2),3},
                    await Foo()
                };
            if (arr2[0][1] == 3 && arr2[1][1] == 2)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }

    public async Task<int[]> Foo()
    {
        await Task.Delay(1);
        return new int[] { 1, 2, 3 };
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run();

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            var expected = @"
0
";
            CompileAndVerify(source, expected, references: new[] { CSharpRef });
        }

        [Fact]
        public void SpillNestedExpressionInArrayInitializer()
        {
            var source = @"
using System;
using System.Threading.Tasks;

class Test
{
    public static async Task<int[,]> Run()
    {
        return new int[,] {
            {1, 2, 21 + (await Task.Factory.StartNew(() => 21)) },
        };
    }

    public static void Main()
    {
        var t = Run();
        t.Wait();
        foreach (var xs in t.Result)
        {
            Console.WriteLine(xs);
        }
    }
}";
            var expected = @"
1
2
42
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillConditionalAccess()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

class Test
{

    class C1
    {
        public int M(int x)
        {
            return x;
        }
    }

    public static int Get(int x)
    {
        Console.WriteLine(""> "" + x);
        return x;
    }

    public static async Task<int> F(int x)
    {
        return await Task.Factory.StartNew(() => x);
    }

    public static async Task<int?> G()
    {
        var c = new C1();
        return c?.M(await F(Get(42)));
    }

    public static void Main()
    {
        var t = G();
        System.Console.WriteLine(t.Result);
    }
}";
            var expected = @"
> 42
42";
            CompileAndVerifyExperimental(source, expected);
        }

        [Fact]
        public void AssignToAwait()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class S
{
    public int x = -1;
}

class Test
{
    static S _s = new S();

    public static async Task<S> GetS()
    {
        return await Task.Factory.StartNew(() => _s);
    }

    public static async Task Run()
    {
        (await GetS()).x = 42;
        Console.WriteLine(_s.x);
    }
}

class Driver
{
    static void Main()
    {
        Test.Run().Wait();
    }
}";
            var expected = @"
42
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void AssignAwaitToAwait()
        {
            var source = @"
using System;
using System.Threading.Tasks;

class S
{
    public int x = -1;
}

class Test
{
    static S _s = new S();

    public static async Task<S> GetS()
    {
        return await Task.Factory.StartNew(() => _s);
    }

    public static async Task Run()
    {
        (await GetS()).x = await Task.Factory.StartNew(() => 42);
        Console.WriteLine(_s.x);
    }
}

class Driver
{
    static void Main()
    {
        Test.Run().Wait();
    }
}";
            var expected = @"
42
";
            CompileAndVerify(source, expected);
        }

        [Fact]
        public void SpillArglist()
        {
            var source = @"
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    static StringBuilder sb = new StringBuilder();
    public async Task Run()
    {
        try
        {
            Bar(__arglist(One(), await Two()));
            if (sb.ToString() == ""OneTwo"")
                Driver.Result = 0;
        }
        finally
        {
            Driver.CompleteSignal.Set();
        }
    }
    int One()
    {
        sb.Append(""One"");
        return 1;
    }
    async Task<int> Two()
    {
        await Task.Delay(1);
        sb.Append(""Two"");
        return 2;
    }
    void Bar(__arglist)
    {
        var ai = new ArgIterator(__arglist);
        while (ai.GetRemainingCount() > 0)
            Console.WriteLine( __refvalue(ai.GetNextArg(), int));
    }
}
class Driver
{
    static public AutoResetEvent CompleteSignal = new AutoResetEvent(false);
    public static int Result = -1;
    public static void Main()
    {
        TestCase tc = new TestCase();
        tc.Run();
        CompleteSignal.WaitOne();

        Console.WriteLine(Result);
    }
}";
            var expected = @"
1
2
0
";
            CompileAndVerify(source, expectedOutput: expected);
        }

        [Fact]
        public void SpillObjectInitializer1()
        {
            var source = @"
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;


struct TestCase : IEnumerable
{
    int X;
    public async Task Run()
    {
        int test = 0;
        int count = 0;
        try
        {
            test++;
            var x = new TestCase { X = await Bar() };
            if (x.X == 1)
                count++;
        }
        finally
        {
            Driver.Result = test - count;
            Driver.CompleteSignal.Set();
        }
    }
    async Task<int> Bar()
    {
        await Task.Delay(1);
        return 1;
    }

    public IEnumerator GetEnumerator()
    {
        throw new System.NotImplementedException();
    }
}
class Driver
{
    static public AutoResetEvent CompleteSignal = new AutoResetEvent(false);
    public static int Result = -1;
    public static void Main()
    {
        TestCase tc = new TestCase();
        tc.Run();
        CompleteSignal.WaitOne();

        Console.WriteLine(Result);
    }
}";
            var expected = @"
0
";
            CompileAndVerify(source, expectedOutput: expected);
        }

        [Fact]
        public void SpillWithByRefArguments01()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class BaseTestCase
{
    public void FooRef(ref decimal d, int x, out decimal od)
    {
        od = d;
        d++;
    }

    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }
}

class TestCase : BaseTestCase
{
    public async void Run()
    {
        int tests = 0;
        try
        {
            decimal d = 1;
            decimal od;

            tests++;
            base.FooRef(ref d, await base.GetVal(4), out od);
            if (d == 2 && od == 1) Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run();

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            CompileAndVerify(source, "0");
        }

        [Fact]
        public void SpillOperator_Compound1()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    public async void Run()
    {
        int tests = 0;
        try
        {
            tests++;
            int[] x = new int[] { 1, 2, 3, 4 };
            x[await GetVal(0)] += await GetVal(4);
            if (x[0] == 5)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run();

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            CompileAndVerify(source, "0");
        }

        [Fact]
        public void SpillOperator_Compound2()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    public async void Run()
    {
        int tests = 0;
        try
        {
            tests++;
            int[] x = new int[] { 1, 2, 3, 4 };
            x[await GetVal(0)] += await GetVal(4);
            if (x[0] == 5)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run();

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            CompileAndVerify(source, "0");
        }

        [Fact]
        public void Async_StackSpill_Argument_Generic04()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class mc<T>
{
    async public System.Threading.Tasks.Task<dynamic> Foo<V>(T t, V u) { await Task.Delay(1); return u; }
}

class Test
{
    static async Task<int> Foo()
    {
        dynamic mc = new mc<string>();
        var rez = await mc.Foo<string>(null, await ((Func<Task<string>>)(async () => { await Task.Delay(1); return ""Test""; }))());
        if (rez == ""Test"")
            return 0;
        return 1;
    }

    static void Main()
    {
        Console.WriteLine(Foo().Result);
    }
}";
            CompileAndVerify(source, "0", references: new[] { CSharpRef });
        }

        [Fact]
        public void AsyncStackSpill_assign01()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

struct TestCase
{
    private int val;
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    public async void Run()
    {
        int tests = 0;

        try
        {
            tests++;
            int[] x = new int[] { 1, 2, 3, 4 };
            val = x[await GetVal(0)] += await GetVal(4);
            if (x[0] == 5 && val == await GetVal(5))
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run();

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            CompileAndVerify(source, "0");
        }

        [Fact]
        public void SpillCollectionInitializer()
        {
            var source = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

struct PrivateCollection : IEnumerable
{
    public List<int> lst; //public so we can check the values
    public void Add(int x)
    {
        if (lst == null)
            lst = new List<int>();
        lst.Add(x);
    }

    public IEnumerator GetEnumerator()
    {
        return lst as IEnumerator;
    }
}

class TestCase
{
    public async Task<T> GetValue<T>(T x)
    {
        await Task.Delay(1);
        return x;
    }

    public async void Run()
    {
        int tests = 0;

        try
        {
            tests++;
            var myCol = new PrivateCollection() { 
                await GetValue(1),
                await GetValue(2)
            };
            if (myCol.lst[0] == 1 && myCol.lst[1] == 2)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test completes, set the flag.
            Driver.CompletedSignal.Set();
        }
    }

    public int Foo { get; set; }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run();

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            CompileAndVerify(source, "0");
        }

        [Fact]
        public void SpillRefExpr()
        {
            var source = @"
using System;
using System.Threading.Tasks;

class MyClass
{
    public int Field;
}

class TestCase
{
    public static int Foo(ref int x, int y)
    {
        return x + y;
    }

    public async Task<int> Run()
    {
        return Foo(
            ref (new MyClass() { Field = 21 }.Field),
            await Task.Factory.StartNew(() => 21));
    }
}

static class Driver
{
    static void Main()
    {
        var t = new TestCase().Run();
        t.Wait();
        Console.WriteLine(t.Result);
    }
}";
            CompileAndVerify(source, "42");
        }

        [Fact]
        public void SpillManagedPointerAssign03()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class TestCase
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(1);
        return t;
    }

    class PrivClass
    {
        internal struct ValueT
        {
            public int Field;
        }

        internal ValueT[] arr = new ValueT[3];
    }

    private PrivClass myClass;

    public async void Run()
    {
        int tests = 0;
        this.myClass = new PrivClass();

        try
        {
            tests++;
            this.myClass.arr[0].Field = await GetVal(4);
            if (myClass.arr[0].Field == 4)
                Driver.Count++;

            tests++;
            this.myClass.arr[0].Field += await GetVal(4);
            if (myClass.arr[0].Field == 8)
                Driver.Count++;

            tests++;
            this.myClass.arr[await GetVal(1)].Field += await GetVal(4);
            if (myClass.arr[1].Field == 4)
                Driver.Count++;

            tests++;
            this.myClass.arr[await GetVal(1)].Field++;
            if (myClass.arr[1].Field == 5)
                Driver.Count++;
        }
        finally
        {
            Driver.Result = Driver.Count - tests;
            //When test complete, set the flag.
            Driver.CompletedSignal.Set();
        }
    }
}

class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static void Main()
    {
        var t = new TestCase();
        t.Run();

        CompletedSignal.WaitOne();
        // 0 - success
        // 1 - failed (test completed)
        // -1 - failed (test incomplete - deadlock, etc)
        Console.WriteLine(Driver.Result);
    }
}";
            CompileAndVerify(source, "0");
        }

        [Fact]
        public void SpillSacrificialRead()
        {
            var source = @"
using System;
using System.Threading.Tasks;

class C
{
    static void F1(ref int x, int y, int z)
    {
        x += y + z;
    }

    static int F0()
    {
        Console.WriteLine(-1);
        return 0;
    }

    static async Task<int> F2()
    {
        int[] x = new int[1] { 21 };
        x = null;
        F1(ref x[0], F0(), await Task.Factory.StartNew(() => 21));
        return x[0];
    }

    public static void Main()
    {
        var t = F2();
        try
        {
            t.Wait();   
        }
        catch(Exception e)
        {
            Console.WriteLine(0);
            return;
        }

        Console.WriteLine(-1);
    }
}";
            CompileAndVerify(source, "0");
        }

        [Fact]
        public void SpillRefThisStruct()
        {
            var source = @"
using System;
using System.Threading.Tasks;

struct s1
{
    public int X;

    public async void Foo1()
    {
        Bar(ref this, await Task<int>.FromResult(42));
    }

    public void Foo2()
    {
        Bar(ref this, 42);
    }

    public void Bar(ref s1 x, int y)
    {
        x.X = 42;
    }
}

class c1
{
    public int X;

    public async void Foo1()
    {
        Bar(this, await Task<int>.FromResult(42));
    }

    public void Foo2()
    {
        Bar(this, 42);
    }

    public void Bar(c1 x, int y)
    {
        x.X = 42;
    }
}

class C
{
    public static void Main()
    {
        {
            s1 s;
            s.X = -1;
            s.Foo1();
            Console.WriteLine(s.X);
        }

        {
            s1 s;
            s.X = -1;
            s.Foo2();
            Console.WriteLine(s.X);
        }

        {
            c1 c = new c1();
            c.X = -1;
            c.Foo1();
            Console.WriteLine(c.X);
        }

        {
            c1 c = new c1();
            c.X = -1;
            c.Foo2();
            Console.WriteLine(c.X);
        }
    }
}";
            var expected = @"
-1
42
42
42
";
            CompileAndVerify(source, expected);
        }

        

        [Fact]
        public void SynthesizedVariables1()
        {
            var source =
@"
using System;
using System.Threading.Tasks;

class C
{
    static void F1(ref int x, int y, int z)
    {
        x += y + z;
    }

    static int F0()
    {
        Console.WriteLine(-1);
        return 0;
    }

    static async Task<int> F2()
    {
        int[] x = new int[1] { 21 };
        x = null;
        F1(ref x[0], F0(), await Task.Factory.StartNew(() => 21));
        F1(ref x[0], F0(), await Task.Factory.StartNew(() => 21));
        return x[0];
    }
}";
            CompileAndVerify(source, additionalRefs: AsyncRefs, options: TestOptions.ReleaseDll.WithMetadataImportOptions(MetadataImportOptions.All), symbolValidator: module =>
            {
                AssertEx.Equal(new[]
                {
                    "<>1__state",
                    "<>t__builder",
                    "<x>5__1",
                    "<>7__wrap1",
                    "<>7__wrap2",
                    "<>u__$awaiter5",
                    "<>7__wrap3",
                }, module.GetFieldNames("C.<F2>d__1"));
            });
#if TODO
            CompileAndVerify(source, additionalRefs: AsyncRefs, options: TestOptions.DebugDll.WithMetadataImportOptions(MetadataImportOptions.All), symbolValidator: module =>
            {
                AssertEx.Equal(new[]
                {
                    "<>1__state",
                    "<>t__builder",
                    "<x>5__1",
                    "<>s__530$1$2",
                    "<>s__530$2$5",
                    "<>s__531$1$1",
                    "<>u__$awaiter4",
                    "<>s__531$2$2",
                }, module.GetFieldNames("C.<F2>d__1"));
            });
#endif
        }
    }
}
