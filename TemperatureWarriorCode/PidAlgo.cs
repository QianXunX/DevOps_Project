using System;

namespace PidAlgo{
    
public class PIDController
{
    private double Kp; // Coeficiente Proporcional
    private double Ki; // Coeficiente Integral
    private double Kd; // Coeficiente Derivativo

    private double integral;
    private double lastError;
    private DateTime lastUpdate;

    // Constructor
    public PIDController(double kp, double ki, double kd)
    {
        this.Kp = kp;
        this.Ki = ki;
        this.Kd = kd;
        this.integral = 0;
        this.lastError = 0;
        this.lastUpdate = DateTime.Now;
    }

    // Método para actualizar el control PID
    public double Update(double setPoint, double actualValue)
    {
        var now = DateTime.Now;
        var timeChange = (now - lastUpdate).TotalSeconds;

        // Calculo del error
        double error = setPoint - actualValue;

        // Cálculo del término integral
        integral += error * timeChange;

        // Cálculo del término derivativo
        double derivative = (error - lastError) / timeChange;

        // Cálculo del valor de salida
        double output = Kp * error + Ki * integral + Kd * derivative;

        // Actualizar variables para la próxima iteración
        lastError = error;
        lastUpdate = now;

        return output;
    }
}

}