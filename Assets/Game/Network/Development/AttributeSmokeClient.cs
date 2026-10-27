#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Attributes.Unity;
using Game.Character;
using Game.Character.Actions;
using Game.Combat.Unity;
using Game.Presentation.UI;
using Unity.Netcode;
using UnityEngine;

namespace Game.Network.Development
{
    public sealed class AttributeSmokeClient : MonoBehaviour
    {
        [Serializable]
        private sealed class ActorReport
        {
            public ulong owner;
            public float health;
            public float maxHealth;
            public float stamina;
            public float maxStamina;
            public bool hasStamina;
            public bool local;
            public Vector3 position;
            public uint attackSequence;
            public uint hitSequence;
            public bool attacking;
            public int attackIndex;
            public string weapon;
            public uint equipmentRevision;
            public bool heldWeapon;
            public bool[] observedSwordAnimations;
            public bool jabAnimation;
            public bool crossAnimation;
            public bool hitAnimation;
            public bool observedHitAnimation;
            public bool observedJabAnimation;
            public bool observedCrossAnimation;
            public bool observedDeathAnimation;
            public bool deathAnimation;
            public float deathNormalizedTime;
        }

        [Serializable]
        private sealed class Report
        {
            public string state;
            public string error;
            public bool ready;
            public bool clientAuthorityDenied;
            public string[] hud;
            public ActorReport[] actors;
            public ActionReport action;
            public bool deathScreenVisible;
            public string[] deathScreenText;
            public int[] observedCountdown;
            public int observedRespawns;
            public EnemyReport[] enemies;
            public SlotReport[] inventory;
            public PickupReport[] pickups;
            public bool inventoryAuthorityDenied;
            public string[] inventoryText;
            public string pickupResult;
            public string operationResult;
            public bool inventoryOpen, inputBlocked;
            public int carriedQuantity;
            public bool inventoryOperationPending;
            public string[] pickupLabels;
            public SlotReport mainHand;
        }

        [Serializable]
        private sealed class SlotReport { public int itemIndex, quantity; }
        [Serializable]
        private sealed class PickupReport { public ulong networkId; public string item; public int quantity; public Vector3 position; public double automaticPickupRemaining; }

        [Serializable]
        private sealed class EnemyReport
        {
            public ulong networkId;
            public string definition;
            public float health, maxHealth;
            public bool dead, authorityDenied, hitAnimation, deathAnimation;
            public bool observedHit, observedDeath, observedAttack;
            public bool colliderEnabled;
            public uint attackSequence;
            public Vector3 position;
            public string[] hud;
            public bool aiNavigationEnabled;
        }

        [Serializable]
        private sealed class ActionControl
        {
            public int sequence;
            public string action;
            public int source, target, quantity;
        }

        [Serializable]
        private sealed class ActionReport
        {
            public int sequence;
            public float peakHeight;
            public float minimumStamina;
            public bool grounded;
            public bool sprinting;
        }

        private string _room;
        private string _reportPath;
        private bool _driveActions;
        private string _action = "idle";
        private int _controlSequence;
        private bool _jumpPending;
        private bool _attackPending;
        private bool _inputDisabled;
        private float _startY;
        private float _peakHeight;
        private float _minimumStamina = float.MaxValue;
        private float _actionStartTime;
        private readonly HashSet<string> _observedAnimations = new HashSet<string>();
        private readonly HashSet<int> _observedCountdown = new HashSet<int>();
        private bool _observedDead;
        private int _observedRespawns;
        private NetworkInventory _observedInventory;
        private string _pickupResult = "";
        private string _operationResult = "";

        private void Update()
        {
            if (!_driveActions || !_inputDisabled || NetworkCharacter.LocalPlayer == null) return;
            NetworkCharacter local = NetworkCharacter.LocalPlayer;
            var respawn = local.GetComponent<NetworkPlayerRespawn>();
            if (respawn != null)
            {
                if (respawn.IsDead) _observedCountdown.Add(Mathf.CeilToInt(respawn.RemainingSeconds));
                else if (_observedDead) _observedRespawns++;
                _observedDead = respawn.IsDead;
            }
            foreach (CharacterCombatController actor in FindObjectsByType<CharacterCombatController>())
            {
                var network = actor.GetComponent<NetworkObject>();
                var animator = actor.GetComponentInChildren<Animator>();
                int layer = animator != null ? 0 : -1;
                if (network == null || !network.IsSpawned || layer < 0 || animator.GetLayerWeight(layer) < .5f) continue;
                var animation = animator.GetCurrentAnimatorStateInfo(layer);
                if (animation.IsName("Base Layer.Reaction.HitChest")) _observedAnimations.Add(network.OwnerClientId + ":hit");
                if (animation.IsName("Base Layer.Combat.Unarmed.PunchJab")) _observedAnimations.Add(network.OwnerClientId + ":jab");
                if (animation.IsName("Base Layer.Combat.Unarmed.PunchCross")) _observedAnimations.Add(network.OwnerClientId + ":cross");
                for (int i = 1; i <= 3; i++) if (animation.IsName("Base Layer.Combat.SingleHandSword.SwordAttack" + i))
                    _observedAnimations.Add(network.OwnerClientId + ":sword" + i);
                if (animation.IsName("Base Layer.Death.Death01")) _observedAnimations.Add(network.OwnerClientId + ":death");
                if (actor.TryGetComponent(out NetworkEnemy enemy))
                {
                    if (animation.IsName("Base Layer.Hit")) _observedAnimations.Add("enemy:" + network.NetworkObjectId + ":hit");
                    if (animation.IsName("Base Layer.Death")) _observedAnimations.Add("enemy:" + network.NetworkObjectId + ":death");
                    if (animation.IsName("Base Layer.Attack")) _observedAnimations.Add("enemy:" + network.NetworkObjectId + ":attack");
                }
            }
            float angle = (Time.unscaledTime - _actionStartTime) * 1.5f;
            Vector3 direction = _action == "sprint" ? new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) : Vector3.zero;
            CharacterHeldButtons held = _action == "sprint" ? CharacterHeldButtons.Sprint : CharacterHeldButtons.None;
            if (_action == "attack") held |= CharacterHeldButtons.Attack;
            CharacterPressedButtons pressed = _jumpPending ? CharacterPressedButtons.Jump : CharacterPressedButtons.None;
            if (_attackPending) pressed |= CharacterPressedButtons.Attack;
            local.ReceiveCommand(local.GetComponent<Game.Input.PlayerInputReader>().GameplayInputBlocked ? default : new CharacterMoveCommand(direction, held, pressed, local.transform.forward));
            _jumpPending = false;
            _attackPending = false;
            _peakHeight = Mathf.Max(_peakHeight, local.transform.position.y - _startY);
            _minimumStamina = Mathf.Min(_minimumStamina, local.GetComponent<CharacterAttributes>().Snapshot.Stamina);
        }

        private void ReadActionControl(NetworkCharacter local)
        {
            if (!_driveActions || local == null) return;
            if (!_inputDisabled)
            {
                local.GetComponent<Game.Input.PlayerInputReader>().enabled = false;
                _inputDisabled = true;
                _startY = local.transform.position.y;
            }
            string path = _reportPath + ".control.json";
            if (!File.Exists(path)) return;
            try
            {
                var control = JsonUtility.FromJson<ActionControl>(File.ReadAllText(path));
                if (control == null || control.sequence <= _controlSequence) return;
                _controlSequence = control.sequence;
                _actionStartTime = Time.unscaledTime;
                _action = control.action == "sprint" || control.action == "attack" ? control.action : "idle";
                _jumpPending = control.action == "jump";
                _attackPending = control.action == "click";
                if (control.action == "pickup")
                {
                    var inventory = local.GetComponent<NetworkInventory>();
                    var pickup = FindObjectsByType<NetworkWorldPickup>().Where(p => p.IsInReach(local.transform, inventory.PickupRange)).OrderBy(p => (p.transform.position - local.transform.position).sqrMagnitude).FirstOrDefault();
                    if (pickup != null) inventory.RequestPickup(pickup);
                }
                if (control.action == "inventory")
                {
                    var inventoryHud = FindFirstObjectByType<InventoryHud>();
                    if (inventoryHud != null) inventoryHud.SetOpen(!inventoryHud.IsOpen);
                }
                if (control.action == "carry" || control.action == "carry_drop" || control.action == "carry_place")
                {
                    var inventoryHud = FindFirstObjectByType<InventoryHud>();
                    if (inventoryHud != null)
                    {
                        if (control.action == "carry") inventoryHud.SelectSlot(control.source, Vector2.zero, false);
                        else if (control.action == "carry_drop") inventoryHud.OutsideClick();
                        else inventoryHud.DropOn(control.target);
                    }
                }
                if (control.action == "move" || control.action == "stale_move")
                {
                    var inventory = local.GetComponent<NetworkInventory>();
                    var expected = control.source == -2 ? inventory.MainHand : inventory.GetSlot(control.source);
                    if (control.action == "stale_move") expected = new Game.Items.ItemStack(expected.ItemIndex, expected.Quantity + 1);
                    inventory.RequestMove(control.source, control.target, control.quantity, expected, control.target == -2 ? inventory.MainHand : inventory.GetSlot(control.target));
                }
                if (control.action == "drop")
                {
                    var inventory = local.GetComponent<NetworkInventory>();
                    inventory.RequestDrop(control.source, control.quantity, control.source == -2 ? inventory.MainHand : inventory.GetSlot(control.source));
                }
                _startY = local.transform.position.y;
                _peakHeight = 0f;
                _minimumStamina = local.GetComponent<CharacterAttributes>().Snapshot.Stamina;
            }
            catch (IOException) { }
            catch (ArgumentException) { }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartRequestedProbe()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-attribute-smoke-room");
            if (index < 0 || index + 1 >= args.Length) return;
            var root = new GameObject("AttributeSmokeClient");
            DontDestroyOnLoad(root);
            var probe = root.AddComponent<AttributeSmokeClient>();
            probe._room = args[index + 1];
            probe._driveActions = Array.IndexOf(args, "-attribute-smoke-actions") >= 0;
            probe._reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "AttributeSmokeReport.json"));
            probe.StartCoroutine(probe.Run());
        }

        private IEnumerator Run()
        {
            float deadline = Time.realtimeSinceStartup + 180f;
            GameSessionController session = null;
            while (session == null && Time.realtimeSinceStartup < deadline)
            {
                session = FindFirstObjectByType<GameSessionController>();
                yield return null;
            }
            if (session == null)
            {
                Write(new Report { state = "failed", error = "No session controller." });
                Application.Quit(1);
                yield break;
            }
            session.JoinOnlineRoom(_room);
            while (Time.realtimeSinceStartup < deadline)
            {
                if (File.Exists(_reportPath + ".quit")) break;
                if (session.State == GameSessionState.Failed)
                {
                    Write(new Report { state = "failed", error = session.Status });
                    break;
                }
                var hud = FindFirstObjectByType<PlayerAttributesHud>();
                var deathScreen = FindAnyObjectByType<PlayerDeathScreen>();
                NetworkCharacter local = NetworkCharacter.LocalPlayer;
                var attributes = local != null ? local.GetComponent<CharacterAttributes>() : null;
                bool ready = session.State == GameSessionState.Playing && attributes != null && attributes.IsReady &&
                    hud != null && ReferenceEquals(hud.BoundSource, attributes) && hud.GetComponent<CanvasGroup>().alpha > 0f;
                if (ready) ReadActionControl(local);
                var inventory = local != null ? local.GetComponent<NetworkInventory>() : null;
                if (inventory != null && _observedInventory != inventory)
                {
                    _observedInventory = inventory;
                    inventory.PickupCompleted += result => _pickupResult = result.ToString();
                    inventory.OperationCompleted += result => _operationResult = result.ToString();
                }
                var inventoryHud = FindFirstObjectByType<InventoryHud>();
                var actors = new List<ActorReport>();
                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null && manager.IsListening && manager.SpawnManager != null)
                    foreach (NetworkObject spawned in manager.SpawnManager.SpawnedObjectsList)
                    {
                        if (spawned.GetComponent<NetworkCharacter>() == null || !spawned.TryGetComponent(out CharacterAttributes actor) || !actor.IsReady) continue;
                        var state = actor.Snapshot;
                        var combat = actor.GetComponent<CharacterCombatController>();
                        var animator = actor.GetComponentInChildren<Animator>();
                        int layer = animator != null ? 0 : -1;
                        AnimatorStateInfo animation = layer >= 0 ? animator.GetCurrentAnimatorStateInfo(layer) : default;
                        actors.Add(new ActorReport
                        {
                            owner = spawned.OwnerClientId, local = spawned.IsOwner,
                            position = spawned.transform.position,
                            health = state.Health, maxHealth = state.Values.MaxHealth,
                            stamina = state.Stamina, maxStamina = state.Values.MaxStamina,
                            hasStamina = state.HasStaminaState,
                            attackSequence = combat != null ? combat.State.AttackSequence : 0,
                            hitSequence = combat != null ? combat.State.HitSequence : 0,
                            attacking = combat != null && combat.State.Attacking,
                            attackIndex = combat != null ? combat.State.AttackIndex : 0,
                            weapon = combat != null && combat.CurrentWeapon != null ? combat.CurrentWeapon.WeaponId : "",
                            equipmentRevision = spawned.GetComponent<NetworkInventory>().EquipmentRevision,
                            heldWeapon = animator != null && animator.GetComponentsInChildren<Transform>().Any(t => t.name == "HeldWeapon"),
                            observedSwordAnimations = Enumerable.Range(1, 3).Select(i => _observedAnimations.Contains(spawned.OwnerClientId + ":sword" + i)).ToArray(),
                            jabAnimation = layer >= 0 && animation.IsName("Base Layer.Combat.Unarmed.PunchJab"),
                            crossAnimation = layer >= 0 && animation.IsName("Base Layer.Combat.Unarmed.PunchCross"),
                            hitAnimation = layer >= 0 && animation.IsName("Base Layer.Reaction.HitChest"),
                            observedHitAnimation = _observedAnimations.Contains(spawned.OwnerClientId + ":hit"),
                            observedJabAnimation = _observedAnimations.Contains(spawned.OwnerClientId + ":jab"),
                            observedCrossAnimation = _observedAnimations.Contains(spawned.OwnerClientId + ":cross"),
                            observedDeathAnimation = _observedAnimations.Contains(spawned.OwnerClientId + ":death"),
                            deathAnimation = layer >= 0 && animation.IsName("Base Layer.Death.Death01"),
                            deathNormalizedTime = layer >= 0 && animation.IsName("Base Layer.Death.Death01") ? animation.normalizedTime : 0f
                        });
                    }
                Write(new Report
                {
                    state = session.State.ToString(), ready = ready,
                    clientAuthorityDenied = attributes != null && !attributes.HasAuthority && !attributes.TryGetAuthority(out _),
                    hud = hud != null ? hud.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text).ToArray() : Array.Empty<string>(),
                    actors = actors.ToArray(),
                    deathScreenVisible = deathScreen != null && deathScreen.IsVisible,
                    deathScreenText = deathScreen != null ? deathScreen.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text).ToArray() : Array.Empty<string>(),
                    observedCountdown = _observedCountdown.OrderByDescending(value => value).ToArray(),
                    observedRespawns = _observedRespawns,
                    enemies = ReadEnemies(),
                    inventory = inventory != null ? Enumerable.Range(0, inventory.SlotCount).Select(i => new SlotReport { itemIndex = inventory.GetSlot(i).ItemIndex, quantity = inventory.GetSlot(i).Quantity }).ToArray() : Array.Empty<SlotReport>(),
                    mainHand = inventory != null ? new SlotReport { itemIndex = inventory.MainHand.ItemIndex, quantity = inventory.MainHand.Quantity } : null,
                    inventoryAuthorityDenied = inventory != null && !inventory.TryGiveItem(inventory.Catalog.Get(0), 1),
                    pickups = FindObjectsByType<NetworkWorldPickup>().Where(p => p.IsSpawned).Select(p => new PickupReport { networkId = p.NetworkObjectId, item = p.Item != null ? p.Item.ItemId : "", quantity = p.Quantity, position = p.transform.position, automaticPickupRemaining = p.AutomaticPickupRemaining }).ToArray(),
                    inventoryText = inventoryHud != null ? inventoryHud.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text).ToArray() : Array.Empty<string>(),
                    pickupResult = _pickupResult,
                    operationResult = _operationResult,
                    inventoryOpen = inventoryHud != null && inventoryHud.IsOpen,
                    carriedQuantity = inventoryHud != null ? inventoryHud.CarriedQuantity : 0,
                    inventoryOperationPending = inventoryHud != null && inventoryHud.OperationPending,
                    inputBlocked = local != null && local.GetComponent<Game.Input.PlayerInputReader>().GameplayInputBlocked,
                    pickupLabels = FindObjectsByType<NetworkWorldPickup>().Where(p=>p.IsSpawned).SelectMany(p=>p.GetComponentsInChildren<UnityEngine.UI.Text>()).Select(t=>t.text).ToArray(),
                    action = ready && _driveActions ? new ActionReport
                    {
                        sequence = _controlSequence, peakHeight = _peakHeight, minimumStamina = _minimumStamina,
                        grounded = local.GetComponent<CharacterMotor>().IsGrounded,
                        sprinting = local.CurrentMovementState.IsSprinting
                    } : null
                });
                yield return new WaitForSecondsRealtime(.2f);
            }
            if (session != null && session.State != GameSessionState.MainMenu) session.ReturnToMainMenu();
            yield return new WaitForSecondsRealtime(2f);
            Application.Quit();
        }

        private void Write(Report report) => File.WriteAllText(_reportPath, JsonUtility.ToJson(report, true));

        private EnemyReport[] ReadEnemies()
        {
            var reports = new List<EnemyReport>();
            foreach (var enemy in FindObjectsByType<NetworkEnemy>())
            {
                var attributes = enemy.GetComponent<CharacterAttributes>();
                if (!enemy.IsSpawned || !attributes.IsReady) continue;
                var animation = enemy.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0);
                var hud = enemy.GetComponentInChildren<EnemyHealthBar>();
                string key = "enemy:" + enemy.NetworkObjectId;
                reports.Add(new EnemyReport {
                    networkId = enemy.NetworkObjectId, definition = enemy.Definition != null ? enemy.Definition.EnemyId : "",
                    health = attributes.Snapshot.Health, maxHealth = attributes.Snapshot.Values.MaxHealth,
                    dead = attributes.Snapshot.IsDead, authorityDenied = !attributes.TryGetAuthority(out _),
                    hitAnimation = animation.IsName("Base Layer.Hit"), deathAnimation = animation.IsName("Base Layer.Death"),
                    observedHit = _observedAnimations.Contains(key + ":hit"), observedDeath = _observedAnimations.Contains(key + ":death"),
                    observedAttack = _observedAnimations.Contains(key + ":attack"), colliderEnabled = enemy.GetComponent<CapsuleCollider>().enabled,
                    attackSequence = enemy.GetComponent<CharacterCombatController>().State.AttackSequence,
                    position = enemy.transform.position,
                    aiNavigationEnabled = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>() != null && enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled,
                    hud = hud != null ? hud.GetComponentsInChildren<UnityEngine.UI.Text>().Select(text => text.text).ToArray() : Array.Empty<string>()
                });
            }
            return reports.ToArray();
        }
    }
}
#endif
