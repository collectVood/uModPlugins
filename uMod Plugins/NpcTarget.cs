namespace Oxide.Plugins
{
    [Info("NPC Target", "Iv Misticos", "1.0.1")]
	[Description("Deny NPCs target other NPCs")]
    class NpcTarget : RustPlugin
    {
        private object OnNpcTarget(BaseNpc npc, BaseEntity entity)
        {
            if (entity.IsNpc || entity is BaseNpc)
                return true;
            return null;
        }

        private object OnNpcPlayerTarget(NPCPlayerApex npcPlayer, BaseEntity entity)
        {
            if (entity.IsNpc || entity is BaseNpc)
                return true;
            return null;
        }
    }
}