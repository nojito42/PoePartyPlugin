using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.MemoryObjects;
using GameOffsets.Native;
using ProcessMemoryUtilities.Managed;
using ProcessMemoryUtilities.Native;
using SharpZydis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace PoePartyPlugin.Memory;
public class MagicInput
{
    #region Structures
    [StructLayout(LayoutKind.Explicit)]
    private struct GemLevelUpControlStruct
    {
        [FieldOffset(0)] public long ElementPtr;
        [FieldOffset(8)] public long CompletedFlag;
    }
    [StructLayout(LayoutKind.Explicit)]
    private struct CastSkillWithTargetControlStruct
    {
        [FieldOffset(0)] public long InIgsPtr;
        [FieldOffset(8)] public long IngameData;
        [FieldOffset(16)] public long EntityPtr;
        [FieldOffset(24)] public long SkillId;
        [FieldOffset(32)] public long CompletedFlag;
    }
    [StructLayout(LayoutKind.Explicit)]
    private struct CastSkillWithPositionControlStruct
    {
        [FieldOffset(0)] public long InIgsPtr;
        [FieldOffset(8)] public Vector2i GridPos;
        [FieldOffset(16)] public long SkillId;
        [FieldOffset(24)] public long CompletedFlag;
    }
    #endregion

    #region Constructors
    public MagicInput(PoePartyPlugin p)
    {
        this.p = p;
        Task.Run(() =>
        {
            try
            {
                castSkillAddress = p.FindPattern(PatternExtensions.CastSkillBytes, PatternExtensions.CastSkillMask, "CastSkillWithTargetPatch");
                castSkillWithPositionAddress = p.FindPattern(PatternExtensions.CastSkillWithPositionBytes, PatternExtensions.CastSkillWithPositionMask, "CastSkillWithPositionPatch");
                gemLevelUpAddress = p.FindPattern(PatternExtensions.GemLevelUpBytes, PatternExtensions.GemLevelUpMask, "GemLevelUpPatch");
                _ready =  castSkillAddress > 0 && castSkillAddress > 0 && gemLevelUpAddress > 0;
            }
            catch (Exception ex)
            {
                DebugWindow.LogError("Init failed: " + ex.Message);
                _ready = false;
            }
        });
        this.p = p;
    }
    #endregion

    #region Fields
    private long castSkillAddress;
    private long castSkillWithPositionAddress;
    private long gemLevelUpAddress;
    private MemoryProtectionFlags oldProtect;
    private bool? _ready;
    private PoePartyPlugin p;

    private record VersionConfig(Dictionary<string, long> Patterns, string Hash, int PluginVersion);
    internal record CloseHandle(nint Handle, nint? Address, int Length, MemoryProtectionFlags OldProtect) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                if (Address.HasValue)
                {
                    NativeWrapper.VirtualProtectEx((IntPtr)Handle, (IntPtr)Address.Value, (IntPtr)Length, OldProtect, out MemoryProtectionFlags val);
                }
                NativeWrapper.CaptureErrors = false;
            }
            finally
            {
                NativeWrapper.CloseHandle((IntPtr)Handle);
            }
        }
    }
    #endregion

    #region Private Methods
    private unsafe void ApplyCastSkillWithTargetPatch()
    {
        if (!_ready.HasValue || _ready == false || p.Settings.PatchMenu.CastSkillWithTargetSettings.PatchFailed || castSkillAddress <= 0)
        {
            return;
        }

        //long num = ((VersionConfig)p.PluginManager.GetStorage(p)["json"]).Patterns["pickup"];
        if (castSkillAddress <= 3072)
        {
            DebugWindow.LogError("Unable to patch ApplyCastSkillWithTargetPatch: unknown offset");
           
            return;
        }
        long value = p.GameController.Window.Process.MainModule.BaseAddress + castSkillAddress;
        string text = $"mov rbx, rcx\n" +
            $"mov rax, qword ptr [rbx]\n" +
            $"mov rcx, rax\n" +
            $"mov rax, qword ptr [rbx+0x8]\n" +
            $"mov rdx, rax\n" +
            $"mov r9, rbx\n" +
            $"add r9, 0x10\n" +
            $"mov rax, {value:X16}\n" +
            $"mov r8, qword ptr [rbx+0x18]\n" +
            $"mov qword ptr [rsp+-0x20], rbx\n" +
            $"sub rsp, 0x1008\n" +
            $"mov rbp, rsp\n" +
            $"mov dword ptr [rsp + 0x20], 2\n" +
            $"mov dword ptr [rsp + 0x28], 0\n" +
            $"call rax\n" +
            $"add rsp, 0x1008\n" +
            $"mov rbx, qword ptr [rsp+-0x20]\n" +
            $"mov qword ptr [rbx+0x20], 1\n" +
            $"ret";
        byte[] array = ZydisCompiler.Compile((IReadOnlyCollection<string>)(object)text.Split('\n'), 0uL);
        using CloseHandle closeHandle = OpenWriteHandle(null, 0);
        nint num2 = NativeWrapper.VirtualAllocEx((IntPtr)closeHandle.Handle, (IntPtr)0, (IntPtr)array.Length, (AllocationType)12288, (MemoryProtectionFlags)64);
        p.Settings.PatchMenu.CastSkillWithTargetSettings.CodePtr.Value = ((IntPtr)num2).ToString("X");
        p.Settings.PatchMenu.CastSkillWithTargetSettings.CodePtrValue = num2;
        fixed (byte* ptr = &array[0])
        {
            void* ptr2 = ptr;
            if (!NativeWrapper.WriteProcessMemory((IntPtr)closeHandle.Handle, (IntPtr)num2, (IntPtr)(nint)ptr2, (IntPtr)array.Length))
            {
                DebugWindow.LogError($"Unable to write ApplyCastSkillWithTargetPatch code: {NativeWrapper.LastError}");
            }
        }
    }
    private unsafe void ApplyCastSkillWithPositionPatch()
    {
        if (!_ready.HasValue || _ready == false|| p.Settings.PatchMenu.CastSkillWithPositionSettings.PatchFailed || castSkillWithPositionAddress <= 0)
        {
            return;
        }
        if (castSkillWithPositionAddress <= 3072)
        {
            DebugWindow.LogError("Unable to patch ApplyCastSkillWithPositionPatch: unknown offset");         
            return;
        }

        long value = p.GameController.Window.Process.MainModule.BaseAddress + castSkillWithPositionAddress;
        string text = $"mov rbx, rcx\n" +
            $"mov rcx, qword ptr [rbx]\n" +
            $"mov r8, qword ptr [rbx + 8]\n" +
            $"mov rax, {value:X16}\n" +
            $"mov rdx, qword ptr [rbx+0x10]\n" +
            $"mov r9, 0\n" +
            $"mov qword ptr [rsp+-0x20], rbx\n" +
            $"sub rsp, 0x1008\n" +
            $"mov rbp, rsp\n" +
            $"call rax\n" +
            $"add rsp, 0x1008\n" +
            $"mov rbx, qword ptr [rsp+-0x20]\n" +
            $"mov qword ptr [rbx+0x18], 1\n" +
            $"ret";
        byte[] array = ZydisCompiler.Compile((IReadOnlyCollection<string>)(object)text.Split('\n'), 0uL);
        using CloseHandle closeHandle = OpenWriteHandle(null, 0);
        nint num2 = NativeWrapper.VirtualAllocEx((IntPtr)closeHandle.Handle, (IntPtr)0, (IntPtr)array.Length, (AllocationType)12288, (MemoryProtectionFlags)64);
        p.Settings.PatchMenu.CastSkillWithPositionSettings.CodePtr.Value = ((IntPtr)num2).ToString("X");
        p.Settings.PatchMenu.CastSkillWithPositionSettings.CodePtrValue = num2;
        fixed (byte* ptr = &array[0])
        {
            void* ptr2 = ptr;
            if (!NativeWrapper.WriteProcessMemory((IntPtr)closeHandle.Handle, (IntPtr)num2, (IntPtr)(nint)ptr2, (IntPtr)array.Length))
            {
                DebugWindow.LogError($"Unable to write ApplyCastSkillWithPositionPatch code: {NativeWrapper.LastError}");
            }
        }
    }
    private int skillId;

    private IntPtr skillidPtr = IntPtr.Zero; // Adresse mémoire pour stocker skillid

    private unsafe void ApplyCastSkillWithPositionPatch2(int initialSkillId = 10505)
    {
        if (!_ready.HasValue || !_ready.Value || p.Settings.PatchMenu.CastSkillWithPosition2Settings.PatchFailed)
            return;

        using CloseHandle handle1 = OpenWriteHandle(null, 0);
        if (handle1.Handle == IntPtr.Zero || handle1.Handle == new IntPtr(-1))
        {
            DebugWindow.LogError("Invalid process handle in ApplyCastSkillWithPositionPatch2.");
            return;
        }

        // Allocate 8 bytes for skillid (should be 8 for qword, not 4 for dword)
        if (skillidPtr == IntPtr.Zero)
        {
            skillidPtr = NativeWrapper.VirtualAllocEx(
                (IntPtr)handle1.Handle,
                IntPtr.Zero,
                (IntPtr)8,
                (AllocationType)0x3000,        // MEM_COMMIT | MEM_RESERVE
                (MemoryProtectionFlags)0x40);  // PAGE_EXECUTE_READWRITE

            if (skillidPtr == IntPtr.Zero)
            {
                DebugWindow.LogError("Failed to allocate memory for skillid storage.");
                return;
            }
        }

        // Write the initial skillid as a long (8 bytes)
        long skillidValue = initialSkillId;
        NativeWrapper.WriteProcessMemory(
            (IntPtr)handle1.Handle,
            skillidPtr,
            ref skillidValue);

        // Game function addresses (update as needed)
        long baseAddr = p.GameController.Window.Process.MainModule.BaseAddress;
        long addr1 = baseAddr + 0x107AD0;
        long addr2 = baseAddr + 0xD14A0;
        long addr3 = baseAddr + 0x1E619C0;

        // The shellcode will read the skillid as a qword (mov rdx, [rax])
        string asmText =

            
    $@"mov rbx, rcx
    mov rcx, qword ptr [rbx]
    mov r8, qword ptr [rbx + 0x8]
    mov rdx, qword ptr [rbx+0x10]
    mov r9, 0
    sub rsp, 0x1008
    mov rbp, rsp
    mov qword ptr [rsp+0x10], rbx
    push rdi
    sub rsp, 0x30
    mov rax, qword ptr [rcx+0x178]
    mov byte ptr [rax+0x361], 1
    movzx ebx, r9b
    movzx edi, dx
    mov qword ptr [rsp+0x40], r8
    add rcx, 0x10
    mov r8b, 1
    xor edx, edx
    mov rax, {addr1:X16}
    call rax
    mov rax, {addr2:X16}
    call rax
    mov rax, {skillidPtr.ToInt64():X16}
    mov rdx, qword ptr [rax]
    lea r9, qword ptr [rsp+0x40]
    mov qword ptr [rsp+0x20], rdi
    xor r8d, r8d
    mov rcx, rax
    mov rax, {addr3:X16}
    call rax
    mov rbx, qword ptr [rsp+0x48]
    add rsp, 0x30
    pop rdi
    mov qword ptr [rbx+0x18], 1
    add rsp, 0x1008
    ret";

        /* "mov rbx, rcx\n" +
    "mov rcx, qword ptr [rbx]\n" +
    "mov r8, qword ptr [rbx + 8]\n" +
    "mov rdx, qword ptr [rbx+0x10]\n" +
    "mov r9, 0\n" +
    "sub rsp, 0x1008\n" +
    "mov rbp, rsp\n" +
    "mov qword ptr [rsp+0x10], rbx\n" +
    "push rdi\n" +
    "sub rsp, 0x30\n" +
    "cmp byte ptr [rsp+0x60], 0\n" +
    "movzx ebx, r9b\n" +
    "movzx edi, dx\n" +
    "jne skipSetByte\n" +
    "mov rax, [rcx+0x178]\n" +
    "mov byte ptr [rax+0x361], 1\n" +
    "skipSetByte:\n" +
    "mov qword ptr [rsp+0x40], r8\n" +
    "add rcx, 0x10\n" +
    "mov r8b, 1\n" +
    "xor edx, edx\n" +
    $"mov rax, 0x{addr1:X}\n" +
    "call rax\n" +
    $"mov rax, 0x{addr2:X}\n" +
    "call rax\n" +*/

        byte[] shellcode = ZydisCompiler.Compile((IReadOnlyCollection<string>)asmText.Split('\n'), 0uL);

        using CloseHandle handle2 = OpenWriteHandle(null, 0);

        nint remoteMem = NativeWrapper.VirtualAllocEx(
            (IntPtr)handle2.Handle,
            IntPtr.Zero,
            (IntPtr)shellcode.Length,
            (AllocationType)0x3000,             // MEM_COMMIT | MEM_RESERVE
            (MemoryProtectionFlags)0x40);       // PAGE_EXECUTE_READWRITE

        if (remoteMem == IntPtr.Zero)
        {
            DebugWindow.LogError("Failed to allocate memory for CastSkillWithPositionPatch2.");
            return;
        }

        fixed (byte* ptr = shellcode)
        {
            if (!NativeWrapper.WriteProcessMemory(
                (IntPtr)handle2.Handle,
                remoteMem,
                (IntPtr)ptr,
                (IntPtr)shellcode.Length))
            {
                DebugWindow.LogError("Failed to write shellcode for CastSkillWithPositionPatch2.");
                return;
            }
        }

        // Save the address for later use
        p.Settings.PatchMenu.CastSkillWithPosition2Settings.CodePtrValue = remoteMem;
        p.Settings.PatchMenu.CastSkillWithPosition2Settings.CodePtr.Value = remoteMem.ToString("X");

        DebugWindow.LogMsg($"ApplyCastSkillWithPositionPatch2 written to 0x{remoteMem:X}");
    }

    // Méthode pour changer le skillid à la volée sans réinjecter le patch
    public void UpdateSkillId(int newSkillId)
    {
        if (skillidPtr == IntPtr.Zero)
        {
            DebugWindow.LogError("skillidPtr not initialized, call ApplyCastSkillWithPositionPatch2 first.");
            return;
        }

        using CloseHandle handle = OpenWriteHandle(null, 0);

        NativeWrapper.WriteProcessMemory(
            (IntPtr)handle.Handle,
            skillidPtr,
            newSkillId,
            4);
    }


    private unsafe void ApplyLevelUpGemPatch()
    {
        if (!_ready.HasValue || _ready == false || p.Settings.PatchMenu.GemLevelUpSettings.PatchFailed || gemLevelUpAddress <= 0)
        {
            return;
        }
        
        if (gemLevelUpAddress <= 3072)
        {
            DebugWindow.LogError("Unable to patch ApplyLevelUpGemPatch: unknown offset");
            return;
        }

        long value = p.GameController.Window.Process.MainModule.BaseAddress + gemLevelUpAddress;
        string text = $"mov rax, {value:X16}\n" +
            $"mov qword ptr [rsp+-0x20], rcx\n" +
            $"sub rsp, 0x1008\n" +
            $"mov rbp, rsp\n" +
            $"call rax\n" +
            $"add rsp, 0x1008\n" +
            $"mov rax, qword ptr [rsp+-0x20]\n" +
            $"mov qword ptr [rax+0x8], 1\n" +
            $"ret";
        byte[] array = ZydisCompiler.Compile((IReadOnlyCollection<string>)(object)text.Split('\n'), 0uL);
        using CloseHandle closeHandle = OpenWriteHandle(null, 0);
        nint num2 = NativeWrapper.VirtualAllocEx((IntPtr)closeHandle.Handle, (IntPtr)0, (IntPtr)array.Length, (AllocationType)12288, (MemoryProtectionFlags)64);
        p.Settings.PatchMenu.GemLevelUpSettings.CodePtr.Value = ((IntPtr)num2).ToString("X");
        p.Settings.PatchMenu.GemLevelUpSettings.CodePtrValue = num2;
        fixed (byte* ptr = &array[0])
        {
            void* ptr2 = ptr;
            if (!NativeWrapper.WriteProcessMemory((IntPtr)closeHandle.Handle, (IntPtr)num2, (IntPtr)(nint)ptr2, (IntPtr)array.Length))
            {
                DebugWindow.LogError($"Unable to write GemLevelUpSettings code: {NativeWrapper.LastError}");
            }
        }
    }
    internal CloseHandle OpenWriteHandle(nint? address, int length)
    {
        nint handle = NativeWrapper.OpenProcess((ProcessAccessFlags)2097151, p.GameController.Memory.Process.Id);
        if (!address.HasValue) return new CloseHandle(handle, null, 0, 0);

        NativeWrapper.VirtualProtectEx((IntPtr)handle, (IntPtr)address.Value, (IntPtr)length, MemoryProtectionFlags.ExecuteReadWrite, out oldProtect);
        return new CloseHandle(handle, address, length, oldProtect);
    }
    #endregion

    #region public Methods
    private void InvokeCastSkillWithTarget(Entity target, uint skillId)
    {
        if (p.Settings.PatchMenu.CastSkillWithTargetSettings.CodePtrValue == 0) throw new Exception("Patch not applied");

        var data = new CastSkillWithTargetControlStruct
        {
            InIgsPtr = p.GameController.IngameState.MouseSettingsPtr,
            IngameData = p.GameController.IngameState.Data.Address,
            EntityPtr = target.Address,
            SkillId = skillId,
            CompletedFlag = 0L
        };

        using var handle = OpenWriteHandle(null, 0);
        int size = Unsafe.SizeOf<CastSkillWithTargetControlStruct>();
        var remoteAddr = NativeWrapper.VirtualAllocEx((IntPtr)handle.Handle, IntPtr.Zero, (IntPtr)size, AllocationType.Commit | AllocationType.Reserve, MemoryProtectionFlags.ReadWrite);
        NativeWrapper.WriteProcessMemory((IntPtr)handle.Handle, checked((IntPtr)remoteAddr), ref data);
        var thread = NativeWrapper.CreateRemoteThread((IntPtr)handle.Handle, IntPtr.Zero, (IntPtr)524288, checked((IntPtr)p.Settings.PatchMenu.CastSkillWithTargetSettings.CodePtrValue), checked((IntPtr)remoteAddr), 0, IntPtr.Zero);
        var status = Kernel32.WaitForSingleObject(thread, 100u);
        if ((int)status == 258) DebugWindow.LogError("Timeout in CastSkillWithTarget");
        else NativeWrapper.VirtualFreeEx((IntPtr)handle.Handle, checked((IntPtr)remoteAddr), IntPtr.Zero, FreeType.Release);
    }

    // 0x29 je veux ecrire     private IntPtr skillidPtr = IntPtr.Zero; // Adresse mémoire pour stocker skillid a castSkillWithPositionAddress + 0x29 aide moi

    public void InvokeCastSkillWithPosition(Vector2i position, uint skillId)
    {
        if (p.Settings.PatchMenu.CastSkillWithPositionSettings.CodePtrValue == 0) throw new Exception("Patch not applied");

        var data = new CastSkillWithPositionControlStruct
        {
            InIgsPtr = p.GameController.IngameState.MouseSettingsPtr,
            GridPos = position,
            SkillId = skillId,
            CompletedFlag = 0L
        };

        using var handle = OpenWriteHandle(null, 0);
        int size = Unsafe.SizeOf<CastSkillWithPositionControlStruct>();
        var remoteAddr = NativeWrapper.VirtualAllocEx((IntPtr)handle.Handle, IntPtr.Zero, (IntPtr)size, AllocationType.Commit | AllocationType.Reserve, MemoryProtectionFlags.ReadWrite);
        NativeWrapper.WriteProcessMemory((IntPtr)handle.Handle, checked((IntPtr)remoteAddr), ref data);
        var thread = NativeWrapper.CreateRemoteThread((IntPtr)handle.Handle, IntPtr.Zero, (IntPtr)524288, checked((IntPtr)p.Settings.PatchMenu.CastSkillWithPositionSettings.CodePtrValue), checked((IntPtr)remoteAddr), 0, IntPtr.Zero);
        var status = Kernel32.WaitForSingleObject(thread, 100u);
        if ((int)status == 258) DebugWindow.LogError("Timeout in CastSkillWithPosition");
        else NativeWrapper.VirtualFreeEx((IntPtr)handle.Handle, checked((IntPtr)remoteAddr), IntPtr.Zero, FreeType.Release);
    }

    public unsafe void InvokeCastSkillWithPosition3(Vector2i position, uint realSkillID, uint skillId = 0x400)
    {
        var poeProcess = p.GameController.Window.Process;

        if (p.Settings.PatchMenu.CastSkillWithPositionSettings.CodePtrValue == 0)
            throw new Exception("Patch not applied");

        var data = new CastSkillWithPositionControlStruct
        {
            InIgsPtr = p.GameController.IngameState.MouseSettingsPtr,
            GridPos = position,
            SkillId = skillId,
            CompletedFlag = 0L
        };

        using var handle = OpenWriteHandle(null, 0);

        var baseAddr = poeProcess.MainModule.BaseAddress;
        var skillIdAddr = IntPtr.Add(baseAddr, (int)(castSkillWithPositionAddress + 0x2A)); // ici on vise *juste* l'immédiat, pas l'opcode

        MemoryProtectionFlags oldProtect;

        // Changer la protection pour écrire
        if (!NativeWrapper.VirtualProtectEx((IntPtr)handle.Handle, skillIdAddr, (IntPtr)4, MemoryProtectionFlags.ExecuteReadWrite, out oldProtect))
        {
            DebugWindow.LogError($"[Patch] Failed to change memory protection: {Marshal.GetLastWin32Error()}");
            return;
        }

        // Écrire uniquement l'immédiat (4 octets) dans l'instruction (mov reg, imm32)
        byte[] skillBytes = BitConverter.GetBytes(realSkillID);
        fixed (byte* ptr = skillBytes)
        {
            if (!NativeWrapper.WriteProcessMemory((IntPtr)handle.Handle, skillIdAddr, (nint)ptr, 4, out _))
            {
                DebugWindow.LogError($"[Patch] Failed to write skill ID immediate: {Marshal.GetLastWin32Error()}");
                NativeWrapper.VirtualProtectEx((IntPtr)handle.Handle, skillIdAddr, 4, oldProtect, out _);
                return;
            }
        }

        NativeWrapper.VirtualProtectEx((IntPtr)handle.Handle, skillIdAddr, 4, oldProtect, out _);

        // Exécution
        int structSize = Unsafe.SizeOf<CastSkillWithPositionControlStruct>();
        IntPtr remoteAddr = NativeWrapper.VirtualAllocEx((IntPtr)handle.Handle, IntPtr.Zero, (IntPtr)structSize,
            AllocationType.Commit | AllocationType.Reserve, MemoryProtectionFlags.ReadWrite);

        NativeWrapper.WriteProcessMemory((IntPtr)handle.Handle, remoteAddr, ref data);

        IntPtr remoteThread = NativeWrapper.CreateRemoteThread((IntPtr)handle.Handle, IntPtr.Zero, (IntPtr)0,
            (IntPtr)p.Settings.PatchMenu.CastSkillWithPositionSettings.CodePtrValue, remoteAddr, 0, IntPtr.Zero);

        var status = Kernel32.WaitForSingleObject(remoteThread, 100);
        if ((int)status == 258)
            DebugWindow.LogError("Timeout waiting for skill cast execution");

        NativeWrapper.VirtualFreeEx((IntPtr)handle.Handle, remoteAddr, IntPtr.Zero, FreeType.Release);

        // ➤ Restaurer l’immédiat d’origine (0x2909)
        if (!NativeWrapper.VirtualProtectEx((IntPtr)handle.Handle, skillIdAddr, (IntPtr)4, MemoryProtectionFlags.ExecuteReadWrite, out oldProtect))
        {
            DebugWindow.LogError($"[Restore] Failed to change protection to restore: {Marshal.GetLastWin32Error()}");
            return;
        }

        byte[] originalBytes = BitConverter.GetBytes((uint)0x2909);
        fixed (byte* ptr = originalBytes)
        {
            if (!NativeWrapper.WriteProcessMemory((IntPtr)handle.Handle, skillIdAddr, (nint)ptr, 4, out _))
            {
                DebugWindow.LogError($"[Restore] Failed to write original bytes: {Marshal.GetLastWin32Error()}");
            }
        }

        NativeWrapper.VirtualProtectEx((IntPtr)handle.Handle, skillIdAddr, (IntPtr)4, oldProtect, out _);
    }


    public void InvokeCastSkillWithPostion2(Vector2i postion, uint skillID1, uint skillID)
    {
        UpdateSkillId((int)skillID);
        if (p.Settings.PatchMenu.CastSkillWithPosition2Settings.CodePtrValue == 0) throw new Exception("Patch not applied");
        var data = new CastSkillWithPositionControlStruct
        {
            InIgsPtr = p.GameController.IngameState.MouseSettingsPtr,
            GridPos = postion,
            SkillId = skillID1,
            CompletedFlag = 0L
        };
        using var handle = OpenWriteHandle(null, 0);
        int size = Unsafe.SizeOf<CastSkillWithPositionControlStruct>();
        var remoteAddr = NativeWrapper.VirtualAllocEx((IntPtr)handle.Handle, IntPtr.Zero, (IntPtr)size, AllocationType.Commit | AllocationType.Reserve, MemoryProtectionFlags.ReadWrite);
        NativeWrapper.WriteProcessMemory((IntPtr)handle.Handle, checked((IntPtr)remoteAddr), ref data);
        var thread = NativeWrapper.CreateRemoteThread((IntPtr)handle.Handle, IntPtr.Zero, (IntPtr)524288, checked((IntPtr)p.Settings.PatchMenu.CastSkillWithPosition2Settings.CodePtrValue), checked((IntPtr)remoteAddr), 0, IntPtr.Zero);
        var status = Kernel32.WaitForSingleObject(thread, 100u);
        if ((int)status == 258) DebugWindow.LogError("Timeout in CastSkillWithPosition2");
        else NativeWrapper.VirtualFreeEx((IntPtr)handle.Handle, checked((IntPtr)remoteAddr), IntPtr.Zero, FreeType.Release);
    }
    private void InvokeGemLevelUp(Element gem)
    {
        if (p.Settings.PatchMenu.GemLevelUpSettings.CodePtrValue == 0) throw new Exception("Patch not applied");

        var data = new GemLevelUpControlStruct
        {
            ElementPtr = gem.Address,
            CompletedFlag = 0L
        };

        using var handle = OpenWriteHandle(null, 0);
        int size = Unsafe.SizeOf<GemLevelUpControlStruct>();
        var remoteAddr = NativeWrapper.VirtualAllocEx((IntPtr)handle.Handle, IntPtr.Zero, (IntPtr)size, AllocationType.Commit | AllocationType.Reserve, MemoryProtectionFlags.ReadWrite);
        NativeWrapper.WriteProcessMemory((IntPtr)handle.Handle, checked((IntPtr)remoteAddr), ref data);
        var thread = NativeWrapper.CreateRemoteThread((IntPtr)handle.Handle, IntPtr.Zero, (IntPtr)524288, checked((IntPtr)p.Settings.PatchMenu.GemLevelUpSettings.CodePtrValue), checked((IntPtr)remoteAddr), 0, IntPtr.Zero);
        var status = Kernel32.WaitForSingleObject(thread, 100u);
        if ((int)status == 258) DebugWindow.LogError("Timeout in GemLevelUp");
        else NativeWrapper.VirtualFreeEx((IntPtr)handle.Handle, checked((IntPtr)remoteAddr), IntPtr.Zero, FreeType.Release);
    }

    public void Tick()
    {
        if (_ready != true) return;
        var pmenu = p.Settings.PatchMenu;
        if (pmenu.CastSkillWithTargetSettings.Enabled && pmenu.CastSkillWithTargetSettings.CodePtrValue == 0)
            ApplyCastSkillWithTargetPatch();

        if (pmenu.CastSkillWithPositionSettings.Enabled && pmenu.CastSkillWithPositionSettings.CodePtrValue == 0)
        {
            ApplyCastSkillWithPositionPatch();
            ApplyCastSkillWithPositionPatch2(10505);
        }
           

        if (pmenu.GemLevelUpSettings.Enabled && pmenu.GemLevelUpSettings.CodePtrValue == 0)
            ApplyLevelUpGemPatch();

        
        return;
    }
    #endregion
}
