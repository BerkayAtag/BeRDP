procedure ValidateBeRdpFirewallScope(const Port, RemoteAddresses: String; Protocol: Integer);
var
  Rule: Variant;
begin
  Rule := CreateOleObject('HNetCfg.FWRule');
  Rule.Protocol := Protocol;
  Rule.LocalPorts := Port;
  Rule.RemoteAddresses := RemoteAddresses;
end;

procedure SetBeRdpFirewallRule(const Name, ProgramPath, Port, RemoteAddresses, ServiceName: String;
  Protocol: Integer);
var
  Policy, Rule: Variant;
begin
  Policy := CreateOleObject('HNetCfg.FwPolicy2');
  Rule := CreateOleObject('HNetCfg.FWRule');
  Rule.Name := Name;
  Rule.Description := 'Managed by BeRDP Setup. Limit access to approved networks.';
  Rule.Grouping := 'BeRDP';
  Rule.ApplicationName := ProgramPath;
  if ServiceName <> '' then
    Rule.ServiceName := ServiceName;
  Rule.Protocol := Protocol;
  Rule.LocalPorts := Port;
  Rule.RemoteAddresses := RemoteAddresses;
  Rule.Direction := 1;
  Rule.Action := 1;
  Rule.Profiles := 3;
  Rule.Enabled := True;
  Policy.Rules.Remove(Name);
  Policy.Rules.Add(Rule);
  Log('Configured firewall rule: ' + Name + ' port=' + Port);
end;

procedure RemoveBeRdpFirewallRule(const Name: String);
var
  Policy: Variant;
begin
  Policy := CreateOleObject('HNetCfg.FwPolicy2');
  Policy.Rules.Remove(Name);
  Log('Removed firewall rule if present: ' + Name);
end;
