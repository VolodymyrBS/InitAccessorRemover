using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Il2Cpp;
using UnityEngine;

namespace InitAccessorRemover
{
    public class InitAccessorRemover : IIl2CppProcessor
    {
        private const string IsExternalInitTypeName = "System.Runtime.CompilerServices.IsExternalInit";

        public int callbackOrder => 0;

        public void OnBeforeConvertRun(BuildReport report, Il2CppBuildPipelineData data)
        {
            foreach (var path in Directory.GetFiles(data.inputDirectory)
                    .Where(p => p.EndsWith(".dll") || p.EndsWith(".exe")))
                ModifiAssembly(path);
        }

        private static void ModifiAssembly(string path)
        {
            try
            {
                using (var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters(ReadingMode.Immediate) { ReadWrite = true }))
                {

                    var isExternalInitType = assembly.MainModule.GetType(IsExternalInitTypeName);
                    assembly.MainModule.TryGetTypeReference(IsExternalInitTypeName, out var isExternalInitTypeRef);

                    if (isExternalInitType == null && isExternalInitTypeRef == null)
                        return;

                    var voidType = assembly.MainModule.TypeSystem.Void;

                    foreach (var type in assembly.MainModule.Types)
                    {
                        foreach (var method in type.Methods)
                        {
                            ModifiMethodDefinitionReturnType(method, isExternalInitType, isExternalInitTypeRef, voidType);

                            if (method.Body?.Instructions == null)
                                continue;

                            foreach (var inst in method.Body.Instructions)
                            {
                                if (inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt)
                                {
                                    var reference = (MethodReference)inst.Operand;
                                    ModifiMethodReferenceReturnType(reference, isExternalInitType, isExternalInitTypeRef, voidType);
                                }
                            }
                        }
                    }

                    assembly.Write();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Can't open {path}. Skip\nDetail:\n{e}");
            }
        }

        private static void ModifiMethodDefinitionReturnType(
            MethodDefinition method,
            TypeDefinition isExternalInitType,
            TypeReference isExternalInitTypeRef,
            TypeReference voidTypeRef)
        {
            if (method == null)
                return;

            if (!(method.ReturnType is RequiredModifierType rmt))
                return;

            var mt = rmt.ModifierType;
            var et = rmt.ElementType;
            if ((mt.Equals(isExternalInitType) || mt.Equals(isExternalInitTypeRef)) && et.Equals(voidTypeRef))
                method.ReturnType = voidTypeRef;
        }

        private static void ModifiMethodReferenceReturnType(
            MethodReference method,
            TypeDefinition isExternalInitType,
            TypeReference isExternalInitTypeRef,
            TypeReference voidTypeRef)
        {
            if (method == null)
                return;

            if (!(method.ReturnType is RequiredModifierType rmt))
                return;

            var mt = rmt.ModifierType;
            var et = rmt.ElementType;
            if ((mt.Equals(isExternalInitType) || mt.Equals(isExternalInitTypeRef)) && et.Equals(voidTypeRef))
                method.ReturnType = voidTypeRef;
        }
    }

}