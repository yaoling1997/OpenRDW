using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InitialConfiguration
{
    public Vector2 initialPosition;
    public Vector2 initialForward;
    public bool isRandom;
    public InitialConfiguration(Vector2 initialPosition, Vector2 initialForward)
    {
        this.initialPosition = initialPosition;
        this.initialForward = initialForward.normalized;
        isRandom = false;
    }
    public InitialConfiguration(bool isRandom) // For Creating Random Configuration or just default of center/up
    {
        this.initialPosition = Vector2.zero;
        this.initialForward = Vector2.up;
        this.isRandom = isRandom;
    }
    public static InitialConfiguration GetDefaultInitialConfiguration() {
        return new InitialConfiguration(Vector2.zero, Vector2.up);
    }
    public static InitialConfiguration Copy(InitialConfiguration initialConfiguration) {
        return new InitialConfiguration(initialConfiguration.initialPosition, initialConfiguration.initialForward);
    }
}
