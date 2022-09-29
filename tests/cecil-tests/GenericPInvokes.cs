using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;

#nullable enable

namespace Cecil.Tests {
	enum GenericCheckResult {
		Ok,
		ContainsGenerics,
		HasSuspectBranchTarget,
		HasWeirdInstruction,
	}

	struct MethodAndResult {
		public MethodDefinition Method;
		public GenericCheckResult Result;
	}

	[TestFixture]
	public class GenericPInvokesTest {
		[TestCaseSource (typeof (Helper), nameof (Helper.NetPlatformImplementationAssemblies))]
		public void CheckSetupBlockUnsafeUsage (string assemblyPath)
		{
			// this scans the specified assmebly for all methods
			// that call SetupBlockUnsafe and then scans the method
			// to see if the first argument to SetupBlockUnsafe is
			// a generic delegate type.
			//
			// the results are not just a list of booleans.
			// some of the results are indeterminant.
			// for example, if the call to SetupBlockUnsafe is
			// also a branch target within the method then all
			// bets are off for what's on the stack. If the call
			// to pass the last argument is also a branch target,
			// then all bets are off. Why? Imagine this scenario:
			// SetupBlockUnsafe (someCondition ? val1 : val2, arg2);
			// This means that the actual argument could be one of
			// two things and it's probably better for a human to
			// look at those cases instead of this code.
			//
			// most of the code generated by the C# compiler is
			// pretty boring, which means that there is a very
			// limited set of instructions that get used to set up
			// this call. They amount to Ldarg, Ldvar, Ldfld variants
			// which we like. The only thing that's "funny" is that
			// when the method is an instance method, then
			// arg 0 is the "this" pointer which is present on the
			// stack, but isn't in the formal parameter this.
			// So there's a little juggling to make sure we don't
			// look past the array of parameters.

			var assembly = Helper.GetAssembly (assemblyPath, readSymbols: true);
			var callsToSetupBlock = AllSetupBlocks (assembly);
			Assert.IsTrue (callsToSetupBlock.Count () > 0);
			var results = callsToSetupBlock.Select (GenericCheckDelegateArgument);
			var allResults = callsToSetupBlock.Zip (results, (m, r) => new MethodAndResult { Method = m, Result = r });
			var failures = allResults.Where (mar => mar.Result != GenericCheckResult.Ok);

			var failingMethods = ListOfFailingGenerics (failures);

			Assert.IsTrue (failures.Count () == 0,
				$"There {failures.Count ()} calls to SetUpBlockUnsafe that have or might have generic delegates as arguments:{failingMethods}\n" +
				"Check each method for usage. If it's a false positive, add the method name to the IsSetupBlockUnsafeOK method in the test.");
		}

		static bool IsSetupBlockUnsafeOK (MethodDefinition method)
		{
			var fullName = method.FullName;
			switch (fullName) {
			default:
				return true;
			}
		}


		[TestCaseSource (typeof (Helper), nameof (Helper.NetPlatformImplementationAssemblies))]
		public void CheckAllPInvokes (string assemblyPath)
		{
			var assembly = Helper.GetAssembly (assemblyPath, readSymbols: true);
			var pinvokes = AllPInvokes (assembly).Where (IsPInvokeOK);
			Assert.IsTrue (pinvokes.Count () > 0);

			var failures = pinvokes.Where (ContainsGenerics).ToList ();
			var failingMethods = ListOfFailingMethods (failures);

			Assert.IsTrue (failures.Count () == 0,
				$"There are {failures.Count ()} pinvoke methods that contain generics. This will not work in .NET 7 and above (see https://github.com/xamarin/xamarin-macios/issues/11771 ):{failingMethods}");
		}

		string ListOfFailingGenerics (IEnumerable<MethodAndResult> methodAndResults)
		{
			var list = new StringBuilder ();
			foreach (var mar in methodAndResults) {
				list.Append ("\n\"").Append (mar.Method.FullName).Append ("\" : ");
				switch (mar.Result) {
				case GenericCheckResult.ContainsGenerics:
					list.Append ("method contains a generic argument for the first arg of SetupBlockUnsafe. This is problematic in .NET 7 and above.");
					break;
				case GenericCheckResult.HasSuspectBranchTarget:
					list.Append ("one of the instructions in calling SetupBlockUnsafe is a branch target which means that checking this usage is indeterminant. Check this call yourself and if it has a generic, fix that. If it's OK then add the fullname of the method (<- that thing in quotes) to the exception method IsSetupBlockUnsafeOK in this test.");
					break;
				case GenericCheckResult.HasWeirdInstruction:
					list.Append ("one of the setup instructions in calling SetupBlockUnsafe is...weird? It was something not expected and probably something that leads to an indeterminant situation. Check this call yourself and if it has a generic, fix that. It it's OK then add the fullname of the method (<- that thing in the quotes) to the exception method IsSetupBlockUnsafeOK in this test.");
					break;
				default:
					list.Append ($"Got an unexpected result from checking - expected an error but got {mar.Result}");
					break;
				}
			}
			return list.ToString ();
		}

		string ListOfFailingMethods (IEnumerable<MethodDefinition> methods)
		{
			var list = new StringBuilder ();
			foreach (var method in methods) {
				list.Append ('\n').Append (method.FullName);
			}
			return list.ToString ();
		}

		static bool ContainsGenerics (MethodDefinition method)
		{
			return method.ContainsGenericParameter;
		}

		IEnumerable<MethodDefinition> AllPInvokes (AssemblyDefinition assembly)
		{
			return Helper.FilterMethods (assembly, method =>
				(method.Attributes & MethodAttributes.PInvokeImpl) != 0);
		}

		static bool IsPInvokeOK (MethodDefinition method)
		{
			var fullName = method.FullName;
			switch (fullName) {
			default:
				return true;
			}
		}

		IEnumerable<MethodDefinition> AllSetupBlocks (AssemblyDefinition assembly)
		{
			return Helper.FilterMethods (assembly, method => {
				if (method.Body is null)
					return false;
				return method.Body.Instructions.Any (IsCallToSetupBlockUnsafe);
			});
		}

		static bool IsCallToSetupBlockUnsafe (Instruction instr)
		{
			return IsCall (instr) && instr.Operand is not null &&
				instr.Operand.ToString () == "System.Void ObjCRuntime.BlockLiteral::SetupBlockUnsafe(System.Delegate,System.Delegate)";
		}

		static bool IsCall (Instruction instr)
		{
			return instr.OpCode == OpCodes.Call ||
				instr.OpCode == OpCodes.Calli;
		}

		static GenericCheckResult GenericCheckDelegateArgument (MethodDefinition method)
		{
			if (method.Body is null)
				return GenericCheckResult.Ok;
			var instrs = method.Body.Instructions;
			for (int i = 0; i < instrs.Count (); i++) {
				if (i > 1 && IsCallToSetupBlockUnsafe (instrs [i])) {
					var penultimate = instrs [i - 1];
					if (!IsLastArgUsageIsReasonable (penultimate))
						return GenericCheckResult.HasWeirdInstruction;
					if (IsBranchTarget (method, penultimate))
						return GenericCheckResult.HasSuspectBranchTarget;

					var instr = instrs [i - 2];
					var typeOfLastArg = GetLastArgType (method, instr);
					if (typeOfLastArg.HasGenericParameters || typeOfLastArg.IsGenericInstance || typeOfLastArg.IsGenericParameter)
						return GenericCheckResult.ContainsGenerics;
					if (IsBranchTarget (method, instrs [i]))
						return GenericCheckResult.HasSuspectBranchTarget;
				}
			}
			return GenericCheckResult.Ok;
		}

		static OpCode [] reasonableOps = new OpCode [] {
			OpCodes.Ldarg_0, OpCodes.Ldarg_1, OpCodes.Ldarg_2,
			OpCodes.Ldarg_3, OpCodes.Ldarg_S, OpCodes.Ldloc_0,
			OpCodes.Ldloc_1, OpCodes.Ldloc_2, OpCodes.Ldloc_3,
 			OpCodes.Ldloc_S
		};

		static bool IsLastArgUsageIsReasonable (Instruction instr)
		{
			return Array.IndexOf (reasonableOps, instr.OpCode) >= 0;
		}

		static bool IsBranchTarget (MethodDefinition method, Instruction target)
		{
			foreach (var instr in method.Body.Instructions) {
				if (IsBranch (instr) && instr.Operand == target)
					return true;
			}
			return false;
		}

		static OpCode [] branches = new OpCode [] {
			OpCodes.Br_S, OpCodes.Brfalse_S, OpCodes.Brtrue_S,
			OpCodes.Beq_S, OpCodes.Bge_S, OpCodes.Bgt_S,
			OpCodes.Ble_S, OpCodes.Blt_S, OpCodes.Bne_Un_S,
			OpCodes.Bge_Un_S, OpCodes.Bgt_Un_S, OpCodes.Ble_Un_S,
			OpCodes.Blt_Un_S, OpCodes.Br, OpCodes.Brfalse,
			OpCodes.Brtrue, OpCodes.Beq, OpCodes.Bge,
			OpCodes.Bgt, OpCodes.Ble, OpCodes.Blt,
			OpCodes.Bne_Un, OpCodes.Bge_Un, OpCodes.Bgt_Un,
			OpCodes.Ble_Un, OpCodes.Blt_Un
		};

		static bool IsBranch (Instruction instr)
		{
			return Array.IndexOf (branches, instr.OpCode) >= 0;
		}

		static TypeReference GetLastArgType (MethodDefinition method, Instruction instr)
		{
			var paramDef = GetOperandType (method, instr);
			if (paramDef is null) {
				throw new NotImplementedException ($"Last instruction before call to SetupBlockUnsafe ({instr.ToString ()}) was not a Ldfld, Ldarg or Ldloc - this is quite unexpected - something's changed in the code base!");
			}
			return paramDef;
		}

		static TypeReference? GetOperandType (MethodDefinition method, Instruction instr)
		{
			var thisOffset = method.IsStatic ? 0 : -1;
			if (instr.OpCode == OpCodes.Ldarg_0)
				return GetCheckParameterType (method, 0 + thisOffset);
			if (instr.OpCode == OpCodes.Ldarg_1)
				return GetCheckParameterType (method, 1 + thisOffset);
			if (instr.OpCode == OpCodes.Ldarg_2)
				return GetCheckParameterType (method, 2 + thisOffset);
			if (instr.OpCode == OpCodes.Ldarg_3)
				return GetCheckParameterType (method, 3 + thisOffset);
			if (instr.OpCode == OpCodes.Ldarg_S)
				return (instr.Operand as ParameterDefinition)?.ParameterType;
			if (instr.OpCode == OpCodes.Ldloc_0)
				return GetLocalType (method, 0);

			if (instr.OpCode == OpCodes.Ldloc_1)
				return GetLocalType (method, 1);

			if (instr.OpCode == OpCodes.Ldloc_2)
				return GetLocalType (method, 2);

			if (instr.OpCode == OpCodes.Ldloc_3)
				return GetLocalType (method, 3);

			if (instr.OpCode == OpCodes.Ldloc_S)
				return (instr.Operand as VariableDefinition)?.VariableType;
			if (instr.OpCode == OpCodes.Ldsfld)
				return (instr.Operand as FieldReference)?.FieldType;

			return null;
		}

		static TypeReference GetCheckParameterType (MethodDefinition method, int index)
		{
			Assert.IsFalse (index >= method.Parameters.Count (),
				"This is unexpected - asked to get parameter {index} from method {method.ToString ()}, but it's not there");

			return method.Parameters [index].ParameterType;
		}

		static TypeReference GetLocalType (MethodDefinition method, int localIndex)
		{
			return method.Body.Variables [localIndex].VariableType;
		}
	}
}
