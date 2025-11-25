namespace BLApi
{
    public interface IAdmin
    {
        void ResetDB();                     // Réinitialiser toute la DB
        void InitializeDB();                // Ré-initialiser avec les données de départ
        DateTime GetClock();                // Lire l’horloge de la simulation
        void ForwardClock(BO.TimeUnit unit); // Avancer l’horloge (minute, heure, jour, etc.)

        BO.Config GetConfig();              // Lire la config (BO.Config)
        void SetConfig(BO.Config config);   // Mettre à jour la config
    }
}
