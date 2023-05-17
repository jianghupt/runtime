# The Jiterpreter

## Introduction
The jiterpreter is a just-in-time compiler for the Mono Interpreter component of the .NET WASM Runtime. It's split into three components:
* The trace compiler, which converts "traces" of sequential Mono Interpreter opcodes into WebAssembly functions
* The interp_entry thunk generator, which generates optimized versions of the wrappers that normally handle transitions from AOT code into the interpreter
* The jit_call thunk generator, which generates optimized versions of the wrapper that normally handles transitions from the interpreter into native code

## Configuring the Jiterpreter
The jiterpreter is configured by mono runtime options, defined in options-def.h. The key ones to configure the jiterpreter are as follows:
* `jiterpreter-traces-enabled`: Enables trace compiler. On by default for single-threaded builds; disabled if threading is active.
* `jiterpreter-interp-entry-enabled`: Enables interp_entry thunks. Only relevant for AOT.
* `jiterpreter-jit-call-enabled`: Enables jit_call thunks. Only relevant for AOT.
* `jiterpreter-stats-enabled`: Enables collecting and printing statistics like number of traces compiled, number of bytes generated, and which interpreter opcodes caused failures. Statistic printing may need to be manually triggered using `INTERNAL.jiterpreter_dump_stats()`.
* `jiterpreter-backward-branches-enabled`: Configures whether backward branches will be handled in traces. If disabled, backward branches will be handled by the interpreter.
* `jiterpreter-eliminate-null-checks`: Configures whether null check elimination is enabled. This optimization may cause correctness issues in rare cases if you hit a bug, especially if your code throws NullReferenceExceptions during normal execution.
* `jiterpreter-wasm-bytes-limit`: Limits the total number of wasm bytes the jiterpreter will generate. Once this limit is hit, it shuts off in order to avoid exhausting browser memory.

To set runtime options when using the .NET runtime directly, use the `withRuntimeOptions` configuration method:
```javascript
    const runtime = await dotnet
        .withRuntimeOptions(["--jiterpreter-stats-enabled"])
```
When using Blazor, you can use the Msbuild property `BlazorWebAssemblyJiterpreter` as a convenient shorthand to configure whether the Jiterpreter is enabled. You can also use `BlazorWebAssemblyRuntimeOptions` to set specific options directly. At present, the Jiterpreter only functions in Blazor applications that have been published. When running with debugging enabled, it will be inactive.

## Trace lifecycle
The trace compiler operates in multiple phases, starting first during code generation for "tiered" (optimized) interpreter methods and then finishing during actual execution of interpreted code.

### Prepare point insertion
While the interpreter is generating code, we select candidate locations that may be good places to create a trace, using multiple heuristics to eliminate locations that are less likely to be valuable. At each candidate location, we insert a "prepare point" opcode that will maintain statistics during execution and eventually trigger compilation of the trace. The heuristics are important because each prepare point opcode adds a small amount of overhead to the method and a large method may have many prepare points. Each prepare point has a unique index with an associated entry in the "trace info" table maintained by the jiterpreter.

### Prepare point execution
During execution, each time a prepare point is hit, we increment its hit counter. If the hit counter reaches a set threshold we will invoke the trace compiler, and if trace compilation is successful we transform the prepare point into a "monitor point". If compilation fails or the trace is rejected for some reason, we patch the opcode to become a NOP, reducing its cost slightly.

### Trace compilation
Trace compilation occurs synchronously as soon as the threshold is hit for a given prepare point and produces a function pointer that can be used to enter the trace directly. Compilation will fail and "reject" a trace if it doesn't meet key criteria - for example, traces that are too short are rejected because the overhead of entering and exiting the trace makes them harmful to performance. Upon successful compilation, the `MINT_TIER_PREPARE_JITERPRETER trace_index` opcode is patched to become `MINT_TIER_MONITOR_JITERPRETER trace_index`.

### Monitor point execution
The trace info table contains a hit counter and "penalty total" for each unique candidate location. During execution when the interpreter hits a monitor point it executes the trace in a special monitoring mode by passing a JiterpreterCallInfo structure into the trace. All traces have special monitoring support compiled in that will update this structure when exiting early, either due to an abnormal condition (like an exception being thrown) or due to a trace compilation failure (an unsupported opcode, etc.) These abnormal exits store an estimated number of opcodes executed along with a backward branch flag, and the combination of these two values are used to compute a "penalty value" for the current trace execution. We maintain a running hit counter for monitoring mode along with a running sum of the penalty values.

After a trace is executed a set number of times in monitoring mode, we compare the average penalty value against a threshold and decide whether to disable the trace (turning the entry point into a NOP) or keep it permanently. This allows us to cull traces that have undesirable behavior we couldn't detect at compile time (for example, conditionally performing a function call 99% of the time right at the beginning of the trace, which forces us to return control to the interpreter.) If the trace survives the monitoring phase, we patch the `MINT_TIER_MONITOR_JITERPRETER trace_index` opcode into a `MINT_TIER_ENTER_JITERPRETER function_pointer` opcode to allow more efficient execution of the trace.

### Normal trace execution
For traces which survive the monitoring phase, interpreter execution will hit a MINT_TIER_ENTER_JITERPRETER opcode, read the function pointer out of it, and directly invoke the function pointer. The trace returns a displacement (in bytes) from the current location, which allows returning control to any location in the method.

## Trace compilation model
The trace compiler is designed to operate sequentially in two simple passes in order to reduce the amount of time and memory consumed by JIT compilation.

### Opcode translation pass
The first pass scans sequentially through the interpreter opcodes for a method, starting from the prepare point, transforming each encountered opcode into one of:
* A native WASM implementation of the opcode
* A call out to a helper that implements the opcode, written in C
* A call out to a libc function that implements the opcode
* A "bailout" which returns control to the interpreter at the opcode's location in order to execute it
During this first pass the compiler also records control flow information, keeping track of branches and branch targets in its "CFG" which will be used later to construct the WASM structures necessary for loops and other control flow.
During compilation a running estimate is maintained of the trace's size, because web browsers impose an arbitrary 4KB limit on the total size of a synchronously compiled module, including the size of things like function names and type information. If we get too close to the 4KB limit, trace compilation will end at the current location. During this phase all of the generated Webassembly code is appended into a scratch buffer, with buffer offsets recorded in the CFG.

### Control flow pass
The second pass generates the final WebAssembly module including metadata like types, imported and exported functions, etc. The actual executable WebAssembly code is stitched together from segments of the scratch buffer based on the "segments" recorded in the CFG. Segments come in three types:
* Blob segments, containing one or more WASM opcodes that execute sequentially. These can be copied directly into the result module
* Branch block header segments, which represent a location targeted by forward or backward branches elsewhere in the code. We generate WebAssembly flow control structures at these locations based on the information we have about the entire trace.
* Branch segments, which represent a conditional or unconditional branch that occurs after a blob. Conditional branch segments are surrounded by header and footer blobs, used to implement opcodes like conditional branches or null checks. These are translated into WebAssembly branch opcodes targeting a specific branch block header, and for backward branches we also set a dispatch index.
For traces containing backward branches, each trace begins with a small "dispatch table" which performs a forward branch to a specific destination determined by a dispatch index. Upon trace entry the dispatch index points to the top of the trace, but when a backwards branch occurs we set a specific dispatch index and always jump to the dispatch table. This is necessary due to WebAssembly's heavily constrained flow control model that does not allow arbitrary jumps and encodes jumps based on nesting depths instead of as branches targeting specific code offsets.

### Opcode translation patterns
Opcodes fall into a few large categories:
* Direct translation - where we generate WebAssembly code matching the behavior of the original interpreter C exactly or almost exactly (perhaps patching out a data_items access to be a constant instead). This describes the vast majority of opcodes. In many cases, this is driven by tables - for example, most of the arithmetic operators are just looked up in a table and generated by a generic emitter function instead of being specific cases in a switch statement.
* Conditional translation - where we can use information available to us at trace compile time to decide whether to omit some or all of the code that would be generated for direct translation. The main example of this is null checks - we are able to remove redundant null checks from sequences of operations that all read from the same local, which results in smaller/faster traces while the interpreter must perform those checks every time.
* Helper translation - some opcodes do a small amount of setup and then call a C helper in the runtime that performs the hard work. This is typically done when translating the opcode directly would generate a large amount of code or the opcode is generally unlikely to be hit. In some cases the interpreter's implementation of an opcode is a thin layer over libc, so for those cases we try to invoke the libc function directly.
* Native translation - some opcodes can be represented by a handful of native WebAssembly opcodes instead of using the C implementation from the interpreter. The main example of this is SIMD operations, but there are a small number of others. This kind of translation needs to be done carefully by consulting the spec because in some cases the semantics are intentionally underspecified.
* Abort - opcodes that can't be implemented in the jiterpreter or haven't been implemented yet will translate into a bailout that terminates trace execution, returning control to the interpreter at the location of the opcode. Many of these bailouts are skipped over by branches, so a given trace might contain multiple bailouts of various types. If an abort is determined to execute unconditionally, we will fully abort trace compilation at that point.

### Opcode values
When determining whether to keep or reject a trace, we do this based on 'opcode values', where each opcode is assigned a value that approximates how much productive work the resulting trace will perform. These opcodes are defined in `jiterpreter-opcode-values.h`. For opcodes like branches we assign them a low value due to the overhead associated with running them in a trace (sometimes worse than the interpreter for reasons described below), while for opcodes like SIMD instructions or field accesses with eliminated null checks, we assign them a high value to represent how they are significantly faster in traces than in the interpreter. The sum of a trace's opcode values is compared against a threshold at the end of trace compilation to decide whether to keep it.

### Trace performance considerations
Transforming interpreter opcodes into WebAssembly opcodes comes with a few key performance considerations:
* Translating each branch opcode into a unique branch in WebAssembly changes branch prediction characteristics (usually for the worse), which without additional optimization may cause performance to decrease. This is because a significant percentage of the branches in interpreter code are for handling error conditions, so in the interpreter they predict accurately ~99% of the time.
* When the interpreter is executing tight loops all of the relevant code and data may remain in cache, while the generated trace for that same code may be much bigger and end up evicting critical information from cache. This increases the importance of generating small, efficient WebAssembly code since the native code generated from it may contain security checks and error handlers.
* Many interpreter opcodes determine their behavior based on information from the method's "data_items" table or opcode arguments, containing things like MonoType* pointers or struct sizes. For optimal performance it is critical to encode this information as constants or where possible use the information to statically determine the correct behavior at compile time. The main example of this is that the jiterpreter unrolls memory sets and moves of known-small sizes, avoiding an expensive call into libc.
* Arbitrary stack or heap access (via pointers) comes with additional overhead in WebAssembly compared to leaving values on the native WASM stack or in WASM locals, due to the need to bounds-check all memory operations. This means that while the C implementation of a given opcode might dereference a pointer multiple times, it can be critical to instead dereference it once and store it into a temporary local. This can cause new problems, however, so careful measurement is necessary to determine whether doing this actually improves performance for a given scenario.
* All major browsers have tiering compilers for WebAssembly, so it is important to ensure that the code we generate will not cause significant performance issues in a given compiler's fast/naive tier(s) (for example, creating an enormous stack frame due to too many locals - this scenario caused stack overflows on iOS at one point.). We should also keep in mind that in some corner cases, our WebAssembly code may itself run in an interpreter.
* Function pointers also have considerably higher overhead in WebAssembly (due to indirection and type checks), so we should take steps where possible to minimize the amount of indirection through vtables and function pointers, calling functions directly (as WebAssembly imports) where possible.

## Interpreter entry thunks
Interpreter entry points that meet some basic criteria (number of arguments, etc) are instrumented to notify the jiterpreter each time they are hit. After a certain number of hits, they are added to the "jit queue" and will be compiled asynchronously in small batches. If a specific entry point is hit an even larger number of times, the queue will immediately be flushed to compile it.

The compiled thunks are simple functions that imitate the behavior of the generic interp_entry wrapper for a given target method, eliminating some of the indirect function calls and branching on types that would normally happen. Arguments are mapped from the native WASM calling convention to the interpreter stack so that execution can begin. The behavior of the thunks is meant to match the semantics of `interp_entry` 1:1.

There is also a small optimization in this path that attempts to detect whether the interp_entry thunk is being used to call a delegate, and in cases where the same delegate is invoked repeatedly we are able to cache the target method instead of performing an expensive delegate invoke lookup every call.

## JIT call thunks
The `do_jit_call` interpreter operation is instrumented to record hit counts for each call site. After a certain number of hits, a call site is added to the "jit queue" and the queue will be flushed asynchronously in small batches. If a specific call site is hit a large number of times, the queue will immediately be flushed.

The compiled jit call thunks are simple functions that load function arguments from the interpreter stack and pass them directly to native code with the WASM calling convention. The thunks also perform exception handling, using native WebAssembly Exceptions if possible for improved performance. The behavior of the thunks is meant to match the semantics of `mini_get_gsharedvt_out_sig_wrapper` 1:1.

Where possible jit call thunks will be "direct", bypassing the by-reference wrappers typically used for AOT->native transitions and calling the target function directly with arguments passed by-value. This is possible when all of the arguments are simple types and the wrapper is not needed for some special reason.

## Feature detection and fallbacks
At startup the jiterpreter will attempt to compile two small webassembly modules in order to detect support for WebAssembly Exception Handling and WebAssembly SIMD. While both of these features are widely supported, they were not available in the WebAssembly MVP so we need to detect their availability before trying to use them. If they are unavailable, code generation will adapt to use fallbacks (for exception handling, a JavaScript helper, and for SIMD, scalar implementations in C or C#.)

## Error handling
The jiterpreter's compiler detects various errors during compilation and reports them to the browser console. If more than a small number of compile errors occur during execution, the jiterpreter will automatically be disabled to avoid having serious impacts on application performance or stability.