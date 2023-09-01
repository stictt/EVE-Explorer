using System.Collections.ObjectModel;

namespace TestForm
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            ObservableCollection<Model> synchronizingCollections = null;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            TestAnalitic testAnalitic = new TestAnalitic();
            var items = await testAnalitic.GetOrder();
            ObservableCollection<Model> synchronizingCollections = new ObservableCollection<Model>(items);
            this.dataGridView1.DataSource = synchronizingCollections;
        }
    }
}