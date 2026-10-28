PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Users (
    Id TEXT PRIMARY KEY,
    Username TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    SecurityStamp TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    IsAdmin INTEGER NOT NULL DEFAULT 0,
    IsActiveDirectoryUser INTEGER NOT NULL DEFAULT 0,
    DomainName TEXT NULL,
    UserPrincipalName TEXT NULL,
    LastLoginAtUtc TEXT NULL
);

CREATE TABLE IF NOT EXISTS Machines (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    IpAddress TEXT NOT NULL UNIQUE,
    RdpHostName TEXT NULL,
    RdpPort INTEGER NOT NULL DEFAULT 3389 CHECK (RdpPort BETWEEN 1 AND 65535)
);

CREATE TABLE IF NOT EXISTS UserMachines (
    Id TEXT PRIMARY KEY,
    UserId TEXT NOT NULL,
    MachineId TEXT NOT NULL,
    CONSTRAINT UQ_UserMachine UNIQUE (UserId, MachineId),
    CONSTRAINT FK_UserMachines_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserMachines_Machine FOREIGN KEY (MachineId) REFERENCES Machines(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS MachineAdGroupEntitlements (
    Id TEXT PRIMARY KEY,
    MachineId TEXT NOT NULL,
    GroupName TEXT NOT NULL,
    DomainName TEXT NULL,
    CONSTRAINT UQ_MachineAdGroupEntitlement UNIQUE (MachineId, GroupName, DomainName),
    CONSTRAINT FK_MachineAdGroupEntitlements_Machine FOREIGN KEY (MachineId) REFERENCES Machines(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS AdminAccessGroups (
    Id TEXT PRIMARY KEY,
    GroupName TEXT NOT NULL,
    DomainName TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    CONSTRAINT UQ_AdminAccessGroup UNIQUE (GroupName, DomainName)
);

CREATE TABLE IF NOT EXISTS SystemSettings (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_UserMachines_UserId ON UserMachines(UserId);
CREATE INDEX IF NOT EXISTS IX_UserMachines_MachineId ON UserMachines(MachineId);
CREATE INDEX IF NOT EXISTS IX_MachineAdGroupEntitlements_MachineId ON MachineAdGroupEntitlements(MachineId);
CREATE INDEX IF NOT EXISTS IX_AdminAccessGroups_GroupName ON AdminAccessGroups(GroupName);
CREATE INDEX IF NOT EXISTS IX_Users_DomainName ON Users(DomainName);
CREATE INDEX IF NOT EXISTS IX_Users_UserPrincipalName ON Users(UserPrincipalName);
