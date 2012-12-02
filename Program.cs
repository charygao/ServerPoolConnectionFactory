using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqToLdap;
using SharpTestsEx;

namespace ServerPoolConnectionFactory
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = new LdapConfiguration();

            var factory = new PooledServerConnectionFactory();
            factory.AddServer(new LdapDirectoryIdentifier("localhost", 389), 1);
            factory.AddServer(new LdapDirectoryIdentifier("computername", 389), 1);

            config.ConfigureCustomFactory(factory);

            var connection1 = config.ConnectionFactory.GetConnection();
            connection1.GetServerName().Satisfy(s => s.Equals("localhost") || s.Equals("computername"));

            var connection2 = config.ConnectionFactory.GetConnection();
            connection2.GetServerName().Satisfy(s => s.Equals("localhost") || s.Equals("computername"));

            Action action = () => config.ConnectionFactory.GetConnection();

            action.Should().Throw<InvalidOperationException>();

            Console.WriteLine("all is well");
            Console.ReadLine();
        }
    }

    public static class Extensions
    {
        public static string GetServerName(this LdapConnection connection)
        {
            return ((LdapDirectoryIdentifier) connection.Directory).Servers[0];
        }
    }
}
