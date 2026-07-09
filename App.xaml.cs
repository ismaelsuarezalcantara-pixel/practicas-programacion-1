using System.Windows;
using PuntoDeVentaFarmacia.Data;

namespace PuntoDeVentaFarmacia
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                DatabaseHelper.Inicializar();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo inicializar la base de datos:\n{ex.Message}",
                    "Error crítico",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}
