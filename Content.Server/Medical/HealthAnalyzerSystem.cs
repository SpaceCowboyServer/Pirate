using Content.Server.Body.Components;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Medical.Components;
using Content.Server.PowerCell;
using Content.Server.Temperature.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.MedicalScanner;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Content.Server.Disease;
using Content.Server.Popups;
using Content.Server.UserInterface;
using Content.Shared.IdentityManagement;

namespace Content.Server.Medical
{
    public sealed class HealthAnalyzerSystem : EntitySystem
    {
        [Dependency] private readonly DiseaseSystem _disease = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly PowerCellSystem _cell = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<HealthAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<HealthAnalyzerComponent, HealthAnalyzerDoAfterEvent>(OnDoAfter);
        }

        private void OnAfterInteract(Entity<HealthAnalyzerComponent> entity, ref AfterInteractEvent args)
        {
            if (args.Target == null || !args.CanReach || !HasComp<MobStateComponent>(args.Target) || !_cell.HasActivatableCharge(entity.Owner, user: args.User))
                return;

            _audio.PlayPvs(entity.Comp.ScanningBeginSound, entity);

            _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(entity.Comp.ScanDelay), new HealthAnalyzerDoAfterEvent(), entity.Owner, target: args.Target, used: entity.Owner)
            {
                BreakOnTargetMove = true,
                BreakOnUserMove = true,
                NeedHand = true
            });
        }

        private void OnDoAfter(Entity<HealthAnalyzerComponent> entity, ref HealthAnalyzerDoAfterEvent args)
        {
            if (args.Handled || args.Cancelled || args.Target == null || !_cell.TryUseActivatableCharge(entity.Owner, user: args.User))
                return;

            _audio.PlayPvs(entity.Comp.ScanningEndSound, args.User);

            UpdateScannedUser(entity, args.User, args.Target.Value, entity.Comp);
            
            // Below is for the traitor item
            // Piggybacking off another component's doafter is complete CBT so I gave up
            // and put it on the same component
            /*
             * this code is cursed wuuuuuuut
             */
            if (string.IsNullOrEmpty(entity.Comp.Disease))
            {
                args.Handled = true;
                return;
            }

            _disease.TryAddDisease(args.Target.Value, entity.Comp.Disease);

            if (args.User == args.Target)
            {
                _popupSystem.PopupEntity(Loc.GetString("disease-scanner-gave-self", ("disease", entity.Comp.Disease)),
                    args.User, args.User);
            }
            else
            {
                _popupSystem.PopupEntity(Loc.GetString("disease-scanner-gave-other", ("target", Identity.Entity(args.Target.Value, EntityManager)),
                    ("disease", entity.Comp.Disease)), args.User, args.User);
            }

            args.Handled = true;
        }

        private void OpenUserInterface(EntityUid user, EntityUid analyzer)
        {
            if (!TryComp<ActorComponent>(user, out var actor) || !_uiSystem.TryGetUi(analyzer, HealthAnalyzerUiKey.Key, out var ui))
                return;

            _uiSystem.OpenUi(ui ,actor.PlayerSession);
        }

        public void UpdateScannedUser(EntityUid uid, EntityUid user, EntityUid? target, HealthAnalyzerComponent? healthAnalyzer)
        {
            if (!Resolve(uid, ref healthAnalyzer))
                return;

            if (target == null || !_uiSystem.TryGetUi(uid, HealthAnalyzerUiKey.Key, out var ui))
                return;

            if (!HasComp<DamageableComponent>(target))
                return;

            float bodyTemperature;
            if (TryComp<TemperatureComponent>(target, out var temp))
                bodyTemperature = temp.CurrentTemperature;
            else
                bodyTemperature = float.NaN;

            float bloodAmount;
            if (TryComp<BloodstreamComponent>(target, out var bloodstream) &&
                _solutionContainerSystem.ResolveSolution(target.Value, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution))
                bloodAmount = bloodSolution.FillFraction;
            else
                bloodAmount = float.NaN;

            OpenUserInterface(user, uid);

            _uiSystem.SendUiMessage(ui, new HealthAnalyzerScannedUserMessage(
                GetNetEntity(target),
                bodyTemperature,
                bloodAmount
            ));
        }
    }
}
