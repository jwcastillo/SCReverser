﻿using Neo;
using Neo.Core;
using Neo.Cryptography.ECC;
using Neo.Implementations.Blockchains.LevelDB;
using Neo.IO.Caching;
using Neo.SmartContract;
using Neo.VM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SCReverser.Core.Interfaces;
using SCReverser.Core.Remembers;
using SCReverser.Core.Types;
using SCReverser.NEO.Internals;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.IO;
using System.Numerics;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace SCReverser.NEO
{
    public class NeoConfig : RememberForm, IInitClassStream, IDisposable
    {
        string _BlockChainPath;

        /// <summary>
        /// Script
        /// </summary>
        public string Script { get; set; }
        /// <summary>
        /// Script
        /// </summary>
        public string Arguments { get; set; }
        /// <summary>
        /// Trigger type
        /// </summary>
        public ETriggerType TriggerType { get; set; } = ETriggerType.Application;
        /// <summary>
        /// Blockchain Path
        /// </summary>
        [Editor(typeof(FolderNameEditor), typeof(UITypeEditor))]
        public string BlockChainPath
        {
            get
            {
                return _BlockChainPath;
            }
            set
            {
                if (_BlockChainPath == value && (Blockchain.Default != null && !(Blockchain.Default is NullBlockChain)))
                    return;

                _BlockChainPath = value;

                if (!EnableBlockChain) return;

                if (Blockchain.Default != null)
                    Blockchain.RegisterBlockchain(new NullBlockChain());

                if (!string.IsNullOrEmpty(value))
                {
                    if (!Directory.Exists(BlockChainPath))
                        Directory.CreateDirectory(BlockChainPath);

                    Blockchain.RegisterBlockchain(new LevelDBBlockchain(value));
                }
            }
        }

        [JsonIgnore]
        public bool EnableBlockChain { get; set; } = false;

        /// <summary>
        /// Fake
        /// </summary>
        public EFake Fake { get; set; } = EFake.Witness | EFake.Storage | EFake.Signature;

        /// <summary>
        /// Constructor
        /// </summary>
        public NeoConfig() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="script">Script</param>
        public NeoConfig(byte[] script) : this()
        {
            Script = Convert.ToBase64String(script);
        }

        /// <summary>
        /// Create Engine from config
        /// </summary>
        public NeoEngine CreateEngine()
        {
            IScriptContainer container = null;

            Block block;
            DataCache<UInt160, AccountState> accounts;
            DataCache<UInt256, AssetState> assets;
            DataCache<UInt160, ContractState> contracts;
            DataCache<StorageKey, StorageItem> storages;

            if (Blockchain.Default != null && !(Blockchain.Default is NullBlockChain))
            {
                // Real Blockchain

                block = Blockchain.Default.GetBlock(Blockchain.Default.Height);
                accounts = Blockchain.Default.GetStates<UInt160, AccountState>();
                assets = Blockchain.Default.GetStates<UInt256, AssetState>();
                contracts = Blockchain.Default.GetStates<UInt160, ContractState>();
                storages = Blockchain.Default.GetStates<StorageKey, StorageItem>();
            }
            else
            {
                // Fake Blockchain

                block = Blockchain.GenesisBlock;
                accounts = new NeoFakeDbCache<UInt160, AccountState>();
                assets = new NeoFakeDbCache<UInt256, AssetState>();
                contracts = new NeoFakeDbCache<UInt160, ContractState>();
                storages = new NeoFakeDbCache<StorageKey, StorageItem>();
            }

            // Create Engine
            IScriptTable script_table = new CachedScriptTable(contracts);
            StateMachine service = new StateMachine(block, accounts, assets, contracts, storages);

            TriggerType t;

            switch (TriggerType)
            {
                case ETriggerType.Application: t = Neo.SmartContract.TriggerType.Application; break;
                case ETriggerType.Verification: t = Neo.SmartContract.TriggerType.Verification; break;
                default: return null;
            }

            return new NeoEngine(t, container, script_table, service, Fake, Fixed8.Zero, true);
        }
        /// <summary>
        /// Get stream
        /// </summary>
        public IEnumerable<StreamModule> GetStream()
        {
            List<StreamModule> ls = new List<StreamModule>();

            int x = 0;
            foreach (string s in new string[] { Arguments, Script })
            {
                x++;
                string name = x == 1 ? "Arguments" : "Script";

                if (string.IsNullOrEmpty(s)) continue;

                Color cl = x == 1 ? Color.FromArgb(50, Color.Violet) : Color.Empty;

                if (UInt160.TryParse(s, out UInt160 hash160))
                {
                    ContractState c = Blockchain.Default.GetContract(hash160);
                    if (c != null)
                    {
                        ls.Add(new StreamModule(name, new MemoryStream(c.Script), false) { Color = cl });
                        continue;
                    }
                }

                try
                {
                    byte[] bhex = s.Replace(" ", "").Replace("\r", "").Replace("\n", "").HexToBytes();
                    ls.Add(new StreamModule(name, new MemoryStream(bhex), false) { Color = cl });
                    continue;
                }
                catch { }

                try
                {
                    JArray array = JArray.Parse(s);
                    if (array.Count > 0)
                    {
                        ScriptBuilder sb = new ScriptBuilder();
                        WriteJArrayToMs(array, sb, false);

                        ls.Add(new StreamModule(name, new MemoryStream(sb.ToArray(), false), false) { Color = cl });
                        continue;
                    }
                }
                catch { }

                try
                {
                    if (File.Exists(s))
                    {
                        ls.Add(new StreamModule(name, File.OpenRead(s), false) { Color = cl });
                        continue;
                    }
                }
                catch { }

                try
                {
                    // Convert from strings

                    List<byte> bytes = new List<byte>();
                    foreach (string op in s.Split(new char[] { ',', ';', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!Enum.TryParse(op, true, out NeoOpCode ret))
                            throw new FormatException();

                        bytes.Add((byte)ret);
                    }

                    if (bytes.Count > 0)
                    {
                        ls.Add(new StreamModule(name, new MemoryStream(bytes.ToArray()), false) { Color = cl });
                        continue;
                    }
                }
                catch { }

                try
                {
                    // Convert from b64
                    byte[] sc = Convert.FromBase64String(s);
                    if (sc != null)
                    {
                        ls.Add(new StreamModule(name, new MemoryStream(sc), false) { Color = cl });
                        continue;
                    }
                }
                catch { }
            }

            return ls.ToArray();
        }

        void WriteJArrayToMs(JArray array, ScriptBuilder ms, bool usePack)
        {
            int m = array.Count;
            for (int x = m - 1; x >= 0; x--)
            {
                JToken o = array[x];
                switch (o.Type)
                {
                    case JTokenType.Integer:
                        {
                            ms.EmitPush(new BigInteger(o.Value<int>()));
                            break;
                        }
                    case JTokenType.String:
                        {
                            string str = o.Value<string>();

                            if (str.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
                            {
                                ms.EmitPush(str.Substring(2).HexToBytes());
                            }
                            else
                            {
                                ms.EmitPush(str);
                            }

                            break;
                        }
                    case JTokenType.Boolean:
                        {
                            ms.EmitPush(o.Value<bool>() ? 1 : 0);
                            break;
                        }
                    case JTokenType.Bytes:
                        {
                            ms.EmitPush(o.Value<byte[]>());
                            break;
                        }
                    case JTokenType.Array:
                        {
                            WriteJArrayToMs(o.Value<JArray>(), ms, true);
                            break;
                        }
                    default: throw (new Exception("Type not allowed: " + o.Type));
                }
            }

            // Pack

            if (!usePack) return;

            ms.EmitPush(m);
            ms.Emit(Neo.VM.OpCode.PACK);
        }

        #region Remember in form
        public override void GetValues(Form f)
        {
            base.GetValues(f);

            if (!(f is FOpen fo)) return;

            fo.txtScript.Text = Script;
            fo.txtArguments.Text = Arguments;
            fo.txtBlockChain.Text = BlockChainPath;
            fo.scriptType.SelectedItem = TriggerType;

            fo.cFakeWitness.Checked = Fake.HasFlag(EFake.Witness);
            fo.cFakeStorage.Checked = Fake.HasFlag(EFake.Storage);
        }
        public override void SaveValues(Form f)
        {
            base.SaveValues(f);

            if (!(f is FOpen fo)) return;

            Script = fo.txtScript.Text;
            Arguments = fo.txtArguments.Text;
            BlockChainPath = fo.txtBlockChain.Text;
            TriggerType = (ETriggerType)fo.scriptType.SelectedItem;

            Fake = EFake.None;

            if (fo.cFakeWitness.Checked) Fake |= EFake.Witness;
            if (fo.cFakeStorage.Checked) Fake |= EFake.Storage;
        }
        #endregion

        /// <summary>
        /// Free resources
        /// </summary>
        public void Dispose()
        {
            if (!EnableBlockChain) return;
            try
            {
                Blockchain.RegisterBlockchain(new NullBlockChain());
            }
            catch { }
        }
    }
}
