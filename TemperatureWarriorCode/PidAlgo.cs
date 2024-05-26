using System;

namespace PidAlgo{
    
public class PIDController
{
    private double Kp; // Coeficiente Proporcional
    private double Ki; // Coeficiente Integral
    private double Kd; // Coeficiente Derivativo

    private double integral;
    private double previousError;
    private DateTime lastUpdateTime;
    private double outputMin = -100.0; // or the appropriate limit
    private double outputMax = 100; // or the appropriate limit


    // Constructor
    public PIDController(double kp, double ki, double kd)
    {
        this.Kp = kp;
        this.Ki = ki;
        this.Kd = kd;
        this.integral = 0;
        this.previousError = 0;
        this.lastUpdateTime = DateTime.Now;
    }

    // Método para actualizar el control PID
    public double Update(double currentValue, double setPoint)
    {
        double error = setPoint - currentValue;
        DateTime now = DateTime.Now;
        double deltaTime = (now - lastUpdateTime).TotalSeconds;
        lastUpdateTime = now;

        // Acumular el error considerando el tiempo
        integral += error * deltaTime;

        // Calcular la tasa de cambio del error considerando el tiempo
        double derivative = (error - previousError) / deltaTime;

        // Calcular la salida del PID
        double output = Kp * error + Ki * integral + Kd * derivative;

        // Anti-windup: Limitar la salida y ajustar la integral si es necesario
        if (output > outputMax)
        {
            output = outputMax;
            integral -= error * deltaTime;  // Restablecer término integral
        }
        else if (output < outputMin)
        {
            output = outputMin;
            integral -= error * deltaTime;  // Restablecer término integral
        }

        previousError = error;
        return output;

        }
    }

}