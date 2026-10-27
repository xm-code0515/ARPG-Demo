namespace Game.Attributes
{
    public enum AttributeType
    {
        MaxHealth,
        MaxStamina,
        AttackPower,
        Defense,
        MoveSpeed
    }

    public enum AttributeModifierOperation
    {
        Flat,
        AdditivePercent,
        Multiply,
        Override
    }
}
