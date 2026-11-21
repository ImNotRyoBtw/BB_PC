using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // Own Windows adapter. Evidence: docs/home-dialog-native.md, BattlePassAnimBar/helper exports
    // 20261104T122836018318Z. No DOTween reference, reactive manager or corrected clock is exposed
    // by this assembly. Linear coroutines and explicit snapshots replace those mechanisms below.
    public sealed class RecoveredBattlePassBar : MonoBehaviour
    {
        private Slider slider;
        private Text sliderText, levelText, revText;
        private GameObject claimTip;
        private Button button;
        private RecoveredBattlePassData currentPass;
        private RecoveredBattlePassProgress progress;
        private bool? haveCompleteTask;
        private float nextRefresh;
        private Coroutine animation;
        private string originalRevText;
        private static int previousPoint = -1;
        private static bool previousClaimVisible;
        public Action OnClickAction;

        public void Initialize(RecoveredFrontend frontend, GameObject root)
        {
            if (frontend == null || root == null) throw new ArgumentNullException("Battle pass host and original root required");
            if (slider != null) throw new InvalidOperationException("Battle pass adapter already initialized");
            if (root.name != "BattlePassBar") throw new InvalidOperationException("Expected original BattlePassBar root, not its parent Slider");
            // Exact authored paths from Resources/Recovered606/ui.json, without descendant search.
            var sliderNode = root.transform.Find("Content/Slider");
            slider = sliderNode == null ? null : sliderNode.GetComponent<Slider>();
            if (slider == null) throw new InvalidOperationException("Original BattlePassBar/Slider component missing");
            sliderText = RequireText(slider.transform, "Text");
            levelText = RequireText(root.transform, "NumText");
            var revNode = root.transform.Find("Content/RevText");
            revText = revNode == null ? null : revNode.GetComponent<Text>();
            if (revText == null) throw new InvalidOperationException("Original BattlePassBar RevText missing");
            var tip = root.transform.Find("ClaimTip");
            if (tip == null) throw new InvalidOperationException("Original BattlePassBar/ClaimTip missing");
            claimTip = tip.gameObject;
            originalRevText = revText.text;
            button = root.GetComponent<Button>();
            // One owned listener, released on destroy. Native InitValueSubscriber attaches lifetime
            // observables repeatedly; absent those observables we do not simulate duplicate delivery.
            if (button != null) button.onClick.AddListener(HandleClick);
            nextRefresh = Time.unscaledTime;
            // Never hide/reparent the root, replace art/layout, or add Slider to a non-Slider object.
        }

        private static Text RequireText(Transform root, string path)
        {
            var node = root.Find(path);
            var text = node == null ? null : node.GetComponent<Text>();
            if (text == null) throw new InvalidOperationException("Original battle pass Text missing: " + path);
            return text;
        }

        private void HandleClick() { OnClickAction?.Invoke(); }
        private void OnEnable()
        {
            nextRefresh = Time.unscaledTime;
            if (slider != null) UpdateRemainingText();
        }
        private void Update()
        {
            if (slider == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1f;
            // Native timer updates presentation only. No polling HTTP request belongs here.
            UpdateRemainingText();
        }
        private void OnDisable()
        {
            if (animation != null) StopCoroutine(animation);
            animation = null;
        }
        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
        }

        // A server DTO is not the reactive manager. The overload accepting HaveCompleteTask keeps
        // that independently observed signal explicit; has_purchased never substitutes for it.
        public void Apply(RecoveredBattlePassData pass) { ApplySnapshot(pass, null); }
        public void Apply(RecoveredBattlePassData pass, bool completeTask) { ApplySnapshot(pass, completeTask); }
        private void ApplySnapshot(RecoveredBattlePassData pass, bool? completeTask)
        {
            if (pass == null) throw new ArgumentNullException(nameof(pass));
            if (slider == null) throw new InvalidOperationException("Initialize original battle pass bindings first");
            var next = RecoveredBattlePassProgress.From(pass);
            bool animate = previousPoint >= 0 && previousPoint != pass.point && isActiveAndEnabled;
            if (animation != null) StopCoroutine(animation);
            animation = null;
            currentPass = pass;
            progress = next;
            haveCompleteTask = completeTask;
            if (animate)
            {
                // ResetToPrev uses helper-local values but displays unlocked.Level + 1.
                int local = RecoveredBattlePassHelpers.GetLevelPoint(pass, next, previousPoint);
                int target = RecoveredBattlePassHelpers.GetLevelTarget(pass, next, previousPoint);
                var unlocked = RecoveredBattlePassHelpers.GetUnlockedModelLevelFromPoint(pass, previousPoint);
                Render(local, target, unlocked.level + 1);
                claimTip.SetActive(previousClaimVisible);
                animation = StartCoroutine(Animate(previousPoint, pass.point, CompleteAnimation));
            }
            else CompleteAnimation();
            // Native TryAnimWithMark marks immediately after scheduling, not when tween completes.
            MakePointMark();
            UpdateRemainingText();
        }

        public void MakePointMark()
        {
            if (currentPass == null || currentPass.point < 0) return;
            previousPoint = currentPass.point;
            previousClaimVisible = claimTip.activeSelf;
        }
        public bool NeedAnimator() { return currentPass != null && previousPoint >= 0 && previousPoint != currentPass.point; }
        public static void ResetMark() { previousPoint = -1; previousClaimVisible = false; }

        private IEnumerator Animate(int from, int to, Action complete)
        {
            int startLevel = RecoveredBattlePassHelpers.GetUnlockingLevelModelFromPoint(currentPass, from).level + 1;
            int finishLevel = RecoveredBattlePassHelpers.GetUnlockingLevelModelFromPoint(currentPass, to).level + 1;
            int count = Math.Max(1, finishLevel - startLevel + 1);
            float duration = 1.8f / count;
            int total = from;
            int level = startLevel;
            // Native completion recurses only while level < finish; decreases are one segment.
            while (true)
            {
                int endpoint = level == finishLevel
                    ? RecoveredBattlePassHelpers.GetLevelPoint(currentPass, progress, to)
                    : RecoveredBattlePassHelpers.GetLevelTarget(currentPass, progress, total);
                float initial = slider.value;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                    slider.value = Mathf.Lerp(initial, endpoint, Mathf.Clamp01(elapsed / duration));
                    UpdateProgressText(); // Native OnUpdate truncates float slider value/max.
                }
                slider.value = endpoint; // Explicit endpoint, including the final frame.
                UpdateProgressText();
                if (level >= finishLevel) break;
                claimTip.SetActive(true);
                total = unchecked(total + endpoint - RecoveredBattlePassHelpers.GetLevelPoint(currentPass, progress, total));
                level++;
                Render(0, RecoveredBattlePassHelpers.GetLevelTarget(currentPass, progress, total), Math.Max(level - 1, 0));
            }
            animation = null;
            // Native final callback initializes subscribers. Here it reapplies one coherent manager
            // snapshot, without claiming to reproduce reactive ordering, lifetimes or duplicate calls.
            complete?.Invoke();
        }

        private void CompleteAnimation()
        {
            Render(progress.Point, progress.Target, progress.DisplayLevel);
            if (haveCompleteTask.HasValue) claimTip.SetActive(haveCompleteTask.Value);
        }
        private void Render(int point, int target, int level)
        {
            slider.maxValue = target;
            slider.value = point;
            // Snapshot callbacks use their integer arguments, not clamped Slider.value.
            sliderText.text = point + " / " + target;
            levelText.text = level.ToString();
        }
        private void UpdateProgressText() { sliderText.text = (int)slider.value + " / " + (int)slider.maxValue; }
        private void UpdateRemainingText()
        {
            if (revText == null || !haveCompleteTask.HasValue) return;
            slider.gameObject.SetActive(!haveCompleteTask.Value);
            revText.fontSize = haveCompleteTask.Value ? 42 : 35;
            if (haveCompleteTask.Value)
            {
                // Both packaged en/es tables resolve this exact key. A missing resolution is not
                // displayed as a key or an invented fallback translation.
                string resolved = RecoveredLocalization.Text("claim_reward_pass_color", null);
                revText.text = resolved ?? originalRevText;
            }
            else
            {
                // TODO: corrected server clock + TimeHelper formatting/keys are not exposed.
                // Keep the timer ticking without fabricating a countdown from the local wall clock.
                revText.text = "";
            }
        }
    }

    // Pure equivalents of the bounded native helper bodies; order and strict comparisons matter.
    // Null model/FreeTask predicate paths intentionally throw instead of silently skipping entries.
    public static class RecoveredBattlePassHelpers
    {
        // TimeHelper 0x2C71D30/0x2C71DA4 takes milliseconds, including the 90-minute boundary.
        internal static string RemainingTime(long milliseconds)
        {
            if(milliseconds<1)return null;
            var time=TimeSpan.FromMilliseconds(milliseconds);
            string key;long value;
            if(time.Days>=2){key="common_end_day";value=time.Days;}
            else if((long)time.TotalMinutes>=90){key="common_end_hour";value=(long)time.TotalHours;}
            else if((long)time.TotalMinutes>=1){key="common_end_min";value=(long)time.TotalMinutes;}
            else {key="common_end_sec";value=(long)time.TotalSeconds;}
            // The shipped Spanish table retains English countdown placeholders; translate these four units locally.
            string template=RecoveredLocalization.Language=="es"
                ?(key=="common_end_day"?(value==1?"_$$_ día":"_$$_ días"):key=="common_end_hour"?(value==1?"_$$_ hora":"_$$_ horas"):key=="common_end_min"?"_$$_ min":"_$$_ s")
                :RecoveredLocalization.Text(key,"_$$_");
            return template.Replace("_$$_",value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        public static List<RecoveredBattlePassLevel> GetAllBattlePassLevels(RecoveredBattlePassData pass)
        {
            if (pass == null) throw new ArgumentNullException(nameof(pass));
            var levels = new List<RecoveredBattlePassLevel>(pass.level_tasks ?? Array.Empty<RecoveredBattlePassLevel>());
            var last = levels.LastOrDefault();
            int endLevel = last == null ? levels.Count : last.level;
            foreach (var task in pass.bonus ?? Array.Empty<RecoveredTaskPeriodItem>())
                levels.Add(new RecoveredBattlePassLevel { level = ++endLevel, free = task, vip = task });
            return levels;
        }
        public static RecoveredBattlePassLevel GetUnlockingLevelModelFromPoint(RecoveredBattlePassData pass, int totalPoint)
        {
            var ordinary = new List<RecoveredBattlePassLevel>(pass.level_tasks ?? Array.Empty<RecoveredBattlePassLevel>());
            var match = ordinary.FirstOrDefault(it => totalPoint < it.free.target);
            if (match != null) return match;
            var all = GetAllBattlePassLevels(pass);
            return all.Skip(ordinary.Count).FirstOrDefault(it => totalPoint < it.free.target) ?? all.LastOrDefault();
        }
        public static RecoveredBattlePassLevel GetUnlockedModelLevelFromPoint(RecoveredBattlePassData pass, int totalPoint)
        {
            var unlocking = GetUnlockingLevelModelFromPoint(pass, totalPoint);
            var all = GetAllBattlePassLevels(pass);
            var last = all.LastOrDefault();
            if (totalPoint >= (last?.free?.target ?? 0)) return last;
            int currentTarget = unlocking?.free?.target ?? 0;
            return all.LastOrDefault(it => (it.free?.target ?? 0) < currentTarget);
        }
        public static bool IsLastLevel(RecoveredBattlePassData pass, RecoveredBattlePassLevel model)
        {
            var last = GetAllBattlePassLevels(pass).LastOrDefault();
            if (model == null) throw new NullReferenceException("Native IsLastLevel requires a model");
            return last != null && model.level == last.level;
        }
        public static int GetLevelPoint(RecoveredBattlePassData pass, RecoveredBattlePassProgress manager, int totalPoint)
        {
            var unlocked = GetUnlockedModelLevelFromPoint(pass, totalPoint);
            bool final = IsLastLevel(pass, unlocked); // Before the later null-task fallback, as native.
            if (final && totalPoint > (unlocked.free?.target ?? 0)) return manager.Point;
            return unlocked.free == null ? 0 : unchecked(totalPoint - unlocked.free.target);
        }
        public static int GetLevelTarget(RecoveredBattlePassData pass, RecoveredBattlePassProgress manager, int totalPoint)
        {
            var unlocking = GetUnlockingLevelModelFromPoint(pass, totalPoint);
            if (unlocking == null) throw new NullReferenceException("Native GetLevelTarget requires an unlocking model");
            var unlocked = GetUnlockedModelLevelFromPoint(pass, totalPoint);
            int target = unlocking.free?.target ?? 0;
            int previous = unlocked?.free?.target ?? 0;
            return IsLastLevel(pass, unlocked) ? manager.Target : unchecked(target - previous);
        }
    }

    // Values published by RefreshUnlockingLevel, without mutating reward status in the server DTO.
    // Empty lists have no newly published values in native; this stateless adapter cannot recover
    // the previous manager values and rejects that case explicitly.
    public sealed class RecoveredBattlePassProgress
    {
        public int Point { get; private set; }
        public int Target { get; private set; }
        public int DisplayLevel { get; private set; }
        public static RecoveredBattlePassProgress From(RecoveredBattlePassData pass)
        {
            var all = RecoveredBattlePassHelpers.GetAllBattlePassLevels(pass);
            if (all.Count == 0) throw new InvalidOperationException("Empty battle pass does not publish a recovered manager progress snapshot");
            int previous = 0;
            for (int index = 0; index < all.Count; index++)
            {
                var level = all[index];
                int target = level?.free?.target ?? 0;
                if (pass.point < target)
                    return new RecoveredBattlePassProgress { Point = unchecked(pass.point - previous), Target = unchecked(target - previous), DisplayLevel = Math.Max(index, 0) };
                // Native reached-threshold paths dereference both tasks (bonus aliases them).
                if (level == null || level.free == null || level.vip == null)
                    throw new NullReferenceException("Native RefreshUnlockingLevel requires reached free/VIP tasks");
                if (index == all.Count - 1)
                    return new RecoveredBattlePassProgress { Point = unchecked(target - previous), Target = unchecked(target - previous), DisplayLevel = index + 1 };
                previous = target;
            }
            throw new InvalidOperationException("Battle pass manager progress unavailable");
        }
    }
}
