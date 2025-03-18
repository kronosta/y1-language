Y1 is a proof-of-concept programming language made to show off a brand new paradigm known as Type-Oriented Programming.

# Name
The name was created while looking for good names that weren't already taken.
A lot of the best names are already used. I recalled probably the worst misspeech I'd ever come across, 
that being saying "Where one?" instead of "Where is it?". Then the thought process began as follows: "Where one" -> "Why one" -> "Y1". 
Y1 appeared to be untaken, and also sounded cool, so I went with it.

# Type-oriented programming
Type-orientation is a derivative of Object-orientation, but instead of focusing on objects, it focuses on types/classes. 
Types have a link to all their instances, and objects are only accessible from their types. 
While typically subclasses have a link to their superclass, in type-oriented programming, 
superclasses have a link to their subclasses, and subclasses are only accessible from their superclass. 
Type-oriented programming also focuses on defining types dynamically for small differences in storage, 
rather than generic types or fields.

# Invocation
Y1's command-line has never been documented before, but I completely redid it from scratch recently after
making Y1's compiler usable in memory using Roslyn to compile the resulting C#. This means that .NET Framework is
no longer officially supported, but there is a "Legacy" compiler that works mostly how it used to with a few
language additions. New features will not be added to these, I implemented the new system to make it more
modular and customizable.

Anyway, the way Y1's command line interface parses arguments is the following:
- If there are exactly two arguments and the first argument is exactly `@@FILE`, the second argument is
  taken as a file path to load arguments from, one per line.
- Otherwise, all recognized arguments begin with a forward slash `/`, then a name, then an equals sign `=`, and then
  a value. If multiple arguments appear with the same name, it's treated as a list of arguments which may be interpreted
  in varying ways.

Currently, the options are:
- `/Source=[File]`, a Y1 source code file. Multiple `/Source` arguments are meant to compile multiple files into one assembly,
though I haven't actually tested that. There must always be at least one `/Source` argument.
- `/Name=[name]`, specifies the assembly name, as well as the name of the folder the assembly gets put in.
  It will silently fall back to the default if you specify this flag more than once, same deal if you omit it.
  The default is `TestY1`.
- `/Version=[tfm];[framework name];[framework version]`, this option doesn't currently specify the .NET target,
  but it does affect the generated `runtimeconfig.json` which defaults to .NET 8.0 if you don't specify.

After invocation, the dll will be compiled and stored into `[Name]/[Name].dll` in the current directory
(the `[Name]` directory will be created if it doesn't already exist). A file called `[Name]/[Name].runtimeconfig.json`
is also generated, this is necessary to run the dll with `dotnet` (the program doesn't generate an executable
so you have to use the .NET tool for now).

# Y1 Syntax and Semantics
A newline sequence is one of: Linefeed, Carriage Return, Form Feed, Vertical Tab.
Each command is normally on its own line, although they can be split across multiple lines in most circumstances 
if the line ends with the line continuation `,,`. Blank lines are completely ignored in the source code. 
Windows line sequences composed of both a Carriage Return and a Linefeed are treated as having a blank line in the middle, 
but blank lines are pretty much removed from the stored-in-memory source-code before it is even processed into C# by the compiler.

## Starting Lines
There is a natural progression of beginning sections (although this only applies after the preprocessor is run). All elements are optional.

1. A line containing `<ATTRIBUTES>` denotes some special content until a line containing `<END_ATTRIBUTES>`.
Inside are `Framework:`, `Platform:`, or `SDK:`, but as of the current update these are ignored unless you
use the legacy compiler through code.
2. A line containing `<REFS>` denotes a list of .dll references until a line containing `<END_REFS>`.
When using the new compiler, a line starting with a single quote loads it automagically from the assembly name,
while other lines will be treated as paths.
3. A line containing `<IMPORTS>` denotes a list of C# using statements without the "using" or the semicolon, until `<END_IMPORTS>`.
4. A line containing `<STANDARD_Y1_LIBS>` denotes a list of standard Y1 libraries to include until `<END_STANDARD_Y1_LIBS>`. 
This is currently only used for the `KeyListener` class which is required for the `ListenForKeys` statement.

In older versions blank lines were accidentally/badly supported between the entries, headers and footers of these.
This has been fixed, and blank lines are supported anywhere in these starting sections. The order still matters though.

## Direct C# Calls
A line beginning with optional whitespace followed by `C# - ` directly compiles to the C# code following it. 
Anything that can be expressed in C# can be used here.

## Comments
A single-line comment starts with `'$.` or `'$:` with optional preceding whitespace. These cannot be on the same line as other code.

A multi-line comment starts with `[#` and ends with `#]`. `[#` can have stuff after it, and `#]` can have stuff before it,
but they must not be on the same line and no regular code can be on the same line. 

Comments are processed AFTER the code is already preprocessed. This means you can use comments to signify data to a `?Rewrite` preprocessor command.

## Top-level classes
Top-level classes don't have a ton of native functionality, but using direct C# calls, you can add basically anything that can be in a class in C#. 
All top-level classes are public.

Note that the class is also implicitly ended, so the class declared last should not be closed explicitly.

Top level classes are started with `@[` followed by the class's name. 
They are typically ended with `]@`, by convention, but can also be ended with `\|`, just like a method.

## Methods
Methods are started with `|/` followed by the method's name. 
They are ended with `\|`.

Method headers also have another form for more control.
```
|/+[arguments]
```
The arguments are separated by double percent signs (`%%`), and follow one of a few accepted forms:
- If an argument starts with `^`, it specifies the C# access modifiers and return type, such as `^public static string`. The `^` will be removed.
  This defaults to `public static void`.
- If an argument starts with `@`, it specifies the name of the method. The `@` stays, so you can use keywords as names without adding an extra `@`.
  This defaults to `Main`.
- If an argument starts with `<`, it specifies generic parameters in typical C# syntax. This defaults to the empty string.
- If an argument starts with `(`, it specifies the normal parameters to the method, in typical C# syntax. This defaults to `()`.
- If an argument starts with `where`, it specifies generic constraints, in typical C# syntax. This defaults to the empty string.

The defaults are arranged in such a way that `public static void Main()` can be written as just `|/+`, and `public static void Main(string[] args)`
can be written as just `|/+(string[] args)`. The full form would be `|/+^public static void%%@Main%%(string[] args)`.

## Variables
Variables are not normally declared explicitly except by C# calls, however there are some important considerations with names. 
Any name beginning with `y1__` is reserved for use by compiled programs. 
New variables should not be declared with these names, but they can be. 
You can also use the pre-existing names for manipulation.

* `y1__aName`: An AssemblyName which normally contains `y1__DynamicAssembly`.
  * If you want to choose the Assembly name for some reason, you can reassign y1__aName, although you will also have to reassign y1__ab and y1__mb.
* `y1__ab`: An AssemblyBuilder with y1__aName as its name and AssemblyBuilderAccess.Run as its run type.
* `y1__mb`: A ModuleBuilder contained within y1__ab which contains all dynamic classes. 
* `y1__il`: The ILGenerator. It gets reassigned every time a new dynamic method is declared, to that method's ILGenerator.
* `y1__stack`: A List<Tuple<Type, Dictionary<string, object>, TypeBuilder, Dictionary<string, Type>>>. 
  * `Item1` is the finished type.
  * `Item2` is the objects of the type.
  * `Item3` is the TypeBuilder while the class is being dynamically defined,.
  * `Item4` is the subclasses. 
* `y1__func`: Intended to hold temporary functions for internal usage. Currently used for summations. Type Object.
* `y1__result`: Intended to hold the temporary result of operations. Currently used for summations. Type Object

All of these variables are followed by an underscore followed by the depth. 
The depth increases inside of internally created lambda expressions, such as summations. 
In normal circumstances, you should refer to `y1__stack` as `y1__stack_0`, for example. 
Here are the operations that cause the depth to increase (usually only inside of the corresponding block):
* `Summation`
* `DoMulti`
* `ListenForKeys`

`y1__arg` is also used without a depth. 
It is only used internally during certain commands, but immediately goes out of scope as soon as the command finished. 
If you declare a variable with this name, many essential commands will throw errors.

Also note that often you can access lower depths then what is currently executing, allowing it to modify mutable values such as `y1__stack`.

## Syntax Errors
Any syntax errors will print a message in the console while compiling so you can tell if something is wrong. 
However, it does not throw an error. 
Any syntax error is simply not compiled, and the compiler will move on to the next line and compile as normal. 


## Commands
### Run mode
#### `PushNew`
Pushes a new element onto `y1__stack`.

#### `DefineType <name>`
Takes the top element on `y1__stack` and initializes the `TypeBuilder` to begin defining a class dynamically. 
By convention, an indented section should be inserted until `FinishType`, but it doesn't have to be.

#### `DefineMethod <name> <var-name>`
Defines the method with name in the TypeBuilder of the top element of `y1__stack`, in addition to saving it to a local variable named var-name. 
This changes it to Method-building mode, which has a different set of commands. 
It also declares a new local scope (The `MethodBuilder` itself it outside that local scope). 
By convention, the stuff in Method-builder mode should be indented, but it doesn't have to be.

#### `FinishType`
Takes the top element on `y1__stack`, and initializes the `Type` to the result of the `TypeBuilder`.

#### `CreateObject <name>`
Takes the top element of `y1__stack`, and declares a new instance. 
Note that since it is stored in a dictionary, names can be overloaded by type in this command, although custom variables do not work this way.

#### `CallMethod <method-name> <object-name>`
Takes the top element of `y1__stack`, takes the instance with key object-name and calls its no-parameter method with method-name.

#### `Roll <depth>`
Takes the nth element from the top of `y1__stack` (1-based), and moves it to the top, removing it from its original spot. 
You can use this to access types lower down on the stack.

#### `ReverseRoll <depth>`
  Reverses the effect of a `Roll` with the same depth. 

#### `Drop`
Removes the top element of `y1__stack`. 
Everything is likely to be garbage collected. 
If you have a top-level type, load it, drop it, and load it again, the instances will be gone.

#### `LoadType <arg>`
Takes the type given by the expression written with `typeof(` + arg + `)`, pushes it onto `y1__stack`, with empty dictionaries and with the TypeBuilder assigned null.

#### `ObjParams`
On the next line should be the object name. 
On the next line should be the parameters. 
Takes the top element of `y1__stack`, creates a new object with a parameterized constructor and gives it the key of the object name. 
The parameters must evaluate to an `object[]`.
Only the parameters support line continuations.

#### MethodParams
On the next line should be the method name. 
On the next line should be the types as a C# expression evaluating to a `Type[]`. 
On the next line should be the object name.
On the next line should be the parameters as a C# expression evaluating to an `object[]`. 
Calls a parameterized method. Only the parameters support line continuations.

#### `DefineField <name> <type>`
Creates a local variable with name that is a field in the `TypeBuilder` of the top element of `y1__stack` with the same name. 
The type should be a C# expression evaluating to a `Type`, and should not have spaces. 
This can be replaced with a C# call for the ability to have the field and local variable have separate names, or to have spaces in the expression.
The field will be public and not static.

#### `DefineParamMethod <name> <var-name>`
On the next line should be a C# expression that evaluates to a `Type[]`. 
Creates a local variable with var-name that is a `MethodBuilder` for a method that is named name, and goes into Method-building mode.

#### `LoadField <name> <var-name>`
Creates a local variable that is the field with name name from the type of the top element of `y1__stack`.

#### `DefineComplexMethod <name> <var-name>`
Next line should be the parameter types. 
Next line should be the return type. 
Next line should be the method attributes (`public`, `private`, and/or `static`)
The mode is then switched to method-building mode.

#### `DefineComplexField <name> <type>`
Next line should be the field attributes (`public`, `private`, and/or `static`).

#### `Subclass <name>`
Takes the top element of the stack and subclasses it. 
The subclass is exclusively accessible through the main class.

#### `DefineSubclassMethod <subclass-name> <method-name> <var-name>`
Like `DefineComplexMethod`, but before the other arguments, the subclass name should be given.

#### `DefineSubclassField <subclass-name> <method-name> <var-name>`
Like `DefineComplexField`, but before the other arguments, the subclass name should be given.

#### `FinishSubclass <subclass-name>`
Uses TypeBuilder.CreateType() to transfer the `TypeBuilder` to a `Type` in the subclasses section of `y1__stack_(depth)`.

#### `CreateSubclassObject <object-name> <subclass-name>`
Creates an object of a subclass. 
The subclass name, enclosed in square brackets, will be automatically inserted before the object name. 
In any further commands, you are required to add the square bracket section.

#### `CallSubclassMethod <method-name> <object-name> <subclass-name>`
Calls a method of a subclass.

#### `Summation <start> <end> <index-name> <type>`
Followed by a block of code ending with `EndSummation`. 
Inside of that block of code, the variable index-name will be assigned an integer. 
Each integer starting at start inclusively and ending at end exclusively will be passed to the block. 
The results of each iteration will all be added up, either using the normal + operation or the `op_Addition` method if it has one. 
It adds them starting with start and then in increasing order, 
(this might matter if the `op_Addition` function is non-associative or non-commutative, or if the block can return multiple different types). 
`y1__result_(outer depth)` will be set to the result of the summation.
The type specifies the result type, it's no longer `dynamic` like it was previously since that isn't well
supported on some platforms.
This function increments the depth while in the block.

#### `DefineVariable <name> <type> <expression>`
Declares a variable with the name and the type, and assigns it the expression. 
There is currently no built-in way to reassign this variable, other than C# calls.

#### `Condition <boolean expression>`
An if block, defining a block until `EndCondition`. Does not currently support else or else if. 
This command does not increase the depth.

#### `While <boolean expression>`
A while loop, defining a block until `EndWhile`. This command does not increase the depth.

#### `DoMulti`
Starts a series of blocks until `EndMulti`, with `AndMulti` between the blocks. 
The blocks will run in separate threads, and the main thread will wait until all of them are done. 
In each block, the depth is incremented. Note that if even one of the blocks is still running, the main thread will not continue. 
You can use `C# - return;` to exit a block prematurely, to make sure they don't get stuck in an infinite loop.

#### `DefineComplexType <name>`
Followed by a line containing the superclass as an expression evaluating to a `Type`. 
Followed by a line containing the interfaces as an expression evaluating to a `Type[]`. 
Followed by a line containing type attributes (`public`, `abstract`, `sealed`, and/or `interface`). Defines a type with the name.

#### `ListenForKeys`
Followed by a list of blocks separated by `KeyCase <ConsoleKeyInfo expression>` lines, ended by `EndListenForKeys`.
The first block will be run for every key, the rest will be run for the respective `ConsoleKeyInfo`. See `examples/KeyListenerTest.y1`.

#### `Return <expression>`
Returns the expression from the method.

### Methodbuilding mode
Methodbuilding mode also has access to `Condition` and `While` commands.

#### `\/`
Ends the local scope for the method and goes back to run mode.

#### `\<arg>`
Normally this emits an opcode. 
It can also have `, ` followed by an argument after it for opcodes with arguments.
However, it simply prefixes arg with `y1__il.Emit(OpCodes.` and suffixes the result with `);`. 
This means if you emit a `Nop` instruction and finish it yourself, you can insert arbitrary C# code into Methodbuilder mode with line continuations. 
You must have the implicit `);` suffix not be a syntax error 
(A good way to do this is to end the C# injection with `//` to comment out the ending in the compiled C#)
This also allows any C# expression to be used for the argument. 
(For any expression for the opcode, you can prefix your C# expression with `Nop == OpCodes.Nop ? (` and suffix with `) : OpCodes.Nop`.)

#### `->(name)`
Declares a local variable named name containing a new label. 
You need to use this before any branch instructions to the label, or `-->` with the label.

#### `-->(name)`
Marks the label stored in the local variable named name. 
You can jump to this point using branch instructiond.

#### `_!<type>`
Declares a local variable in the dynamic method, with the type dictated by the result of a C# `Type` expression in type. 
If the first character of type is `!`, it automatically inserts it into `typeof()`. 
You can still have the first bit of the expression be a logical not by having whitespace before the exclamation point.

#### `<TRY>(label-name)`
Declares the start of the try part of an exception handling block, with a local variable with name label-name attached to a label at the start here. 
The label is automatically marked, so you cannot jump forward to it, although you can jump back. 
(A way to deal with this is to have the try block at the beginning, after a goto to a label after the try block, but declared before the goto.

#### `<CATCH>(type)`
Declares the start of a catch block, with exception type type. 
The type is automatically enclosed within typeof(), but this can be circumvented to be a full C# Type expression by prefixing with `int) == typeof(int) ? (` and suffixing with `) : typeof(int`.

#### `<FINALLY>`
Declares the start of a finally block. Remember that it must be ended with the `Endfinally` opcode.

#### `<FAULT>`
Declares the start of a fault block. Remember that it must be ended with the `Endfinally` opcode.

#### `<FILTER>`
Declares the start of a filter block. Remember that it must be ended with the `Endfilter` opcode.

#### `<END>`
Ends an exception block.

By convention, the area inside of exception blocks not including the starting and ending instructions themselves should be indented, although it doesn't have to be.

# Preprocessor
The preprocessor uses lines starting with a question mark.

Some directives take a block as an argument. These are always ended by a line containing a single question mark.
Inside of a preprocessor block, `?!` is replaced by `?`. This of course means that `?!!` is replaced by just `?!`, allowing you to nest preprocessor blocks
when that is necessary.

The preprocessor runs in cycles, running over and over again until there are no more lines starting with a question mark.
This means preprocessor directives can manufacture other preprocessor directives.

## Grave escapes
Certain preprocessor directives use grave escapes, which use the grave accent mark/backtick (`` ` ``) as an escape.

The following escape sequences are available:
Escapes:
- `` `E ``, `!`
- `` `Q ``, `?`
- `` `S ``, space
- `` `N ``, newline
- `` `T ``, tab
- `` `R ``, carriage return
- `` `} ``, `]`
- `` `G ``, `` ` ``
- `` `Uxxxx `` (where x is a hexadecimal digit) - UTF-16 unicode codepoint
- (`` ` `` followed by a newline, carriage return, form feed, or vertical tab),
  escapes a newline. This only matters in very specific scenarios, such as
  when grave escaping in Yen interpolations or if you somehow inject line breaks
  into a line.

## Post-processing
Preprocessor directives have a tendency to trim whitespace off the input,
which in very specific situations might matter (plus I want my preprocessor to
be more general-purpose). Also, the way it was previously set up, question marks
could never be the first non-whitespace character in a line.

To fix this, an extra step gets applied after all Preprocessing cycles finish and
no lines are left starting with question marks. This step will also be applied inside
of `?PreprocessorEnclose` blocks.

When a line contains only `[<?>]` surrounded by optional whitespace, it takes the
first non-whitespace character of the next line to decide what to do:
- `Q` - Replace the Q with a question mark
- `W` - The rest of the line should be two comma-separated integers,
  the first being the number of leading spaces and the second being the number of trailing spaces.
  Consume one more line and apply the whitespace.
- `G` - The most powerful out of all of them, it uses the rest of the line as a grave-escaped string,
  allowing you to mix tabs, spaces, question marks, odd Unicode characters, etc.

To escape `[<?>]`, you can use W since it treats the third line literally:
```
[<?>]
W0,0
[<?>]
```

#### `?File <filename>`
  Reads the file into the source code for the next cycle. (Somewhat like #include in C/C++).

#### `?Define <name>`
```
?Define <name>
<block>
?
```
Defines the long macro with the name as the block. On each line, everything preceding and including the first colon is considered a comment.
When called, `?n?` will be replaced with the nth argument.
For example:
```
?Define Print
  Print the line :C# - Console.WriteLine("?1?")
?
```

The `?!` -> `?` replacement works a bit differently, as it happens at the `?Call`
and not at the define. This means that `?!2?!` can be used to encode `?2?`, for example.

#### `?Call <name> !!arg1!!arg2!!arg3!!etc.`
Calls the long macro with the name. Grave escapes are supported
for inserting exclamation points.

#### `?DefineShort <name> <content>`
Defines a short macro. They have the same `?n?` substitution and can be called with `[[name !!arg1!!arg2!!etc.]]`.
Grave escapes are supported for inserting exclamation points.
Note that short macro calls cannot be deferred with `?Defer`, and the substitution applies immediately across the entire remaining code,
including within strings and comments.

One way of deferring a short macro call is to do something like this:
```
?Define DeferredShort
    :?!1?!ShortMacro !!argument 1!!argument 2]]
?
?Call DeferredShort !![[
```

If you want to defer it twice you can just defer all of those likes like this:
```
?Defer ?Define DeferredShort
?Defer   :?!1?!ShortMacro !!argument 1!!argument 2]]
?Defer ?
?Defer ?Call DeferredShort !![[
```

Note that these methods will not apply the short macro later (it has already been run and never will again).
In order to apply it, you will also need to defer another copy of the `?DefineShort` command.
The best use of these methods is simply to escape the short macro so it can be used literally in the code.

Also note that `?ShortMacro`, due to the way it works internally, will immediately restart the preprocessor cycle
with the macro applied to everything after this call, and all directives ignored until the new cycle.
This means that the short macro can be used in other directives, but it also means that the later directives will effectively have
one extra `?Defer` and the earlier ones will run again before getting to this point.

A weird trick if you want comments on the same line as other code is to use:
```
?DefineShort Comment ?1?
```

Now you can just write something like:
```
[[Comment !!!!This is C# code.]] C# - Console.WriteLine("hi world!");
```

Effectively that replaces your comment with the empty string, as long as you put 4 exclamation points (not two).

#### `?Rewrite <Out|Err>`
```
?Rewrite <Out|Err>
<block>
?
<block>
?
```
Rewrites the second block with either the standard output or standard error of the first when supplied the second block as standard input.
This compiles and runs the first block separately, so too many of these can make compiling slow.

#### `?Undefine <name>`
Undefines the long macro with the name.
You cannot undefine a short macro because it's never really defined in the first place,
it has immediate and permanent effects on the code in the same cycle and then doesn't do anything anymore.

#### `?IfDefined <name>`
```
?IfDefined <name>
<block>
?
<block>
?
```
If the long macro with the name is defined, it will preprocess/include/compile the first block, otherwise the second.

#### `?Defer <line>`
Defers the line to the next cycle. Mainly useful for directives and their blocks so that they have an effect later.
You can stack these indefinitely.

#### `?WriteToFile <filename> <macro name>`
Writes the contents of the macro with the name to the file. No arguments (`?n?`) are substituted.

#### `?PreprocessorEnclose`
```
?PreprocessorEnclose
<block>
?
```
Runs the block in a separate environment with different macros (none of them carry over in either direction.) The inside still has `?!` replaced with `?`, so watch out for that.

#### `?CondenseLines <lines>`
Allows you to have multiple lines in a single line, since unpreprocessed Y1 has only one command per line with continuations.
The lines are separated by `!!`. They support grave escapes.
So:
```
?CondenseLines !!DefineVariable Str "hello"!!C# - Console.WriteLine(Str + "`E`G" + "`GE");
```
Effectively means:
```
DefineVariable Str "hello"
C# - Console.WriteLine(Str + "!`" + "`E");
```
This also defers each of the lines to the next cycle, so if you put a macro in here, it won't immediately be executed.

#### `?User_Diagnostic <message>`
Prints the message to the console, with grave escapes supported.

#### `?User_Read`
Reads a single character from the user (at preprocessing time, not runtime) and puts it into a queue.

#### `?User_IfChar <char>`
```
?User_IfChar <char>
<block>
?
<block>
?
```
Looks at the front character of the input queue (without dequeueing it) and checks it against the given character, with grave escapes supported.

#### `?User_DequeueInput`
Dequeues a character from the input queue.

#### `?RunPre2Processor`
This takes one argument, which is two binary digits. If the first is 1, the lines of the following block will be stripped of whitespace. If the second is 1,
`?!` will be replaced with `?`.

What follows is a normal Preprocessor block. The stuff inside will be joined together by newlines and fed through the Prepreprocessor (with 2 "pre"s),
then split by linefeeds, carriage returns, vertical tabs, and form feeds, back into the multi-string 1-per-line format.

#### `?DeferN`
Like `?Defer`, but before the directive/line to defer, there's a number of times that it should be deferred. This is mainly just to save users the pain of
stacking `?Defers` into horrifically long lines, but it accomplishes the same thing.

#### `?PrintPPResults`
Simply prints the lines already outputted during the current preprocessor cycle. Thus, it is advised that you put this directive at the bottom of your source code file.
Also, it only does this for one cycle, so you have to DeferN some copies if you want more (alternatively you could set up some sort of self-replicating thing with ?Rewrite).

#### `?ConcatLines` and `?ConcatLinesGrave`
```
?ConcatLines[Grave]
  <block>
?
```

Concatenates all the lines in the block into one line. If you use `?ConcatLinesGrave`, the lines will each decode grave escape sequences.

# Prepreprocessor
A prepreprocessor exists that processes the input in terms of strings and regex rather than lines. 
You can call these functions with the yen symbol (`¥`), followed by any number of symbols other than the closing square bracket (`]`), followed by a closing square bracket. 
Note that this doesn't really work on ANSI-encoded documents, so you'll have to encode it in UTF-8 or UTF-16. 
Also, if the program begins with `^^$`, any instances of `###Yen;` will be replaced with the yen symbol BEFORE prepreprocessing, meaning you can use this in ASCII documents.

## Char escapes
`¥\(base 10 character code)]` escapes the character notated by the character code.

## Shell commands
`¥$(stuff)]` is a bit complex:
- The stuff between `$` and `]` is split into section by semicolons (`;`).
- Each section is split by a colon (`:`) into a stack-based language subsection with commands delimited by spaces, and a command consisting of two parts, the executable name and one argument (on Windows, this means the full argument structure; on Unix systems, this is pretty hard to deal with, but using an external shell script or executable works, so does using an awk call with a literal program (since that can use just a single argument)), separated by a space, with grave escapes for each.
- The stack based language has the following commands:
  - `@FileExists` : pops a string off the stack, if a file with that name/path exists, push "true", otherwise "false"
  - `@OSVersion` : pops a regex, tests if it matches the result of `Environment.OSVersion.Platform.ToString() + "-" + Environment.OSVersion.Version.ToString()`, pushes "true" if it is a match, otherwise "false"
  - `@OSVersionMatch` : pops a string off the stack containing an operator concatenated with a representation of the version number. The operator can be `=` (equal), `>` (greater than), `<` (less than), `{` (less than or equal), or `}` (greater than or equal). The string popped goes on the right side, while the OS version goes on the left. Returns "true" if a match, otherwise "false".
  - `@And` : pops two values, either "true" or "false", does an AND gate, pushes the result
  - `@Or` : pops two values, either "true" or "false", does an OR gate, pushes the result
  - `@Not` : pops a value, either "true" or "false", does a NOT gate, pushes the result
  - Anything else : pushes it to the stack
- After the stack-based language finishes, a value is popped from the top of the stack. If it is exactly "true", the command will be run, with its output being placed into the program where this prepreprocessor directive used to be.
 
## Pre(pre)processor calls
`¥P1(stuff)]` calls the Preprocessor (with 1 "pre"). It's interpreted as a list of lines separated by `!`, each supporting full grave escapes.
This is one of the situations where the line continuation feature of grave escapes comes in handy. The preprocessor will run on it and the lines will
join together with \n in between them.

`¥P2(stuff])` calls the Prepreprocessor (with 2 "pre"s). It's interpreted as one big grave-escaped string which gets prepreprocessed and stuck right back in.


