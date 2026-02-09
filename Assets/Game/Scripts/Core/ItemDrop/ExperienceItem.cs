public class ExperienceItem : DropItem<ExperiencePoint> {
    public override void Pick() {
        ExperienceEvent.Trigger(ExperienceEventType.Consume, (int)data);
        base.Pick();
    }
}
