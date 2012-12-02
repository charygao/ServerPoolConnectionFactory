using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Net;
using LinqToLdap;

namespace ServerPoolConnectionFactory
{

    public class PooledServerConnectionFactory : ILdapConnectionFactory
    {
        private readonly ConcurrentDictionary<string, ServerPoolMemberConnectionFactory> _servers;

        public PooledServerConnectionFactory()
        {
            _servers = new ConcurrentDictionary<string, ServerPoolMemberConnectionFactory>(StringComparer.OrdinalIgnoreCase);
        }

        public void AddServer(LdapDirectoryIdentifier identifier, int maxConnections, int protocolVersion = 3, bool ssl = false, double? timeout = null, NetworkCredential credentials = null, AuthType? authType = null)
        {
            var serverName = identifier.Servers[0];
            var factory = new LdapConnectionFactory(serverName);
            if (credentials != null)
                factory.AuthenticateAs(credentials);
            if (authType.HasValue)
                factory.AuthenticateBy(authType.Value);

            if (timeout.HasValue)
                factory.ConnectionTimeoutIn(timeout.Value);

            factory.ProtocolVersion(protocolVersion);

            if (identifier.FullyQualifiedDnsHostName)
                factory.ServerNameIsFullyQualified();

            if (identifier.Connectionless)
                factory.UseUdp();

            if (ssl) factory.UseSsl();

            factory.UsePort(identifier.PortNumber);

            _servers[serverName] = new ServerPoolMemberConnectionFactory(serverName, factory, maxConnections); 
        }

        public void RemoveServer(string serverName)
        {
            ServerPoolMemberConnectionFactory factory;
            if (!_servers.TryRemove(serverName, out factory))
            {
                throw new InvalidOperationException(serverName + " not found.");
            }
        }

        public LdapConnection GetConnection()
        {
            var factory = _servers.FirstOrDefault(c => c.Value.CanConnect);

            if (Equals(factory, default(KeyValuePair<string, ServerPoolMemberConnectionFactory>))) 
                throw new InvalidOperationException("No connections available");

            return factory.Value.GetConnection();
        }

        public void ReleaseConnection(LdapConnection connection)
        {
            var server = ((LdapDirectoryIdentifier)connection.Directory).Servers[0];

            _servers[server].ReleaseConnection(connection);
        }

        private class ServerPoolMemberConnectionFactory : ILdapConnectionFactory
        {
            private ILdapConnectionFactory _factory;
            private int _maxConnections;
            private int _connectionCount;
            private string _serverName;

            public ServerPoolMemberConnectionFactory(string serverName, ILdapConnectionFactory factory, int maxConnections)
            {
                _serverName = serverName;
                _factory = factory;
                _maxConnections = maxConnections;
            }

            public bool CanConnect { get { return _maxConnections > _connectionCount; } }

            public LdapConnection GetConnection()
            {
                if (!CanConnect) throw new InvalidOperationException("Too many connections for " + _serverName);
                
                var connection = _factory.GetConnection();
                _connectionCount++;

                return connection;
            }

            public void ReleaseConnection(LdapConnection connection)
            {
                _factory.ReleaseConnection(connection);
                _connectionCount--;
            }
        }
    }
}
