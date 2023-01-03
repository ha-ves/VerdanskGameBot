﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VerdanskGameBot.GameServer.Db;

#nullable disable

namespace VerdanskGameBot.Migrations.MySql
{
    [DbContext(typeof(GameServerMySqlDb))]
    [Migration("20230103021258_MySql_PK_Fix")]
    partial class MySql_PK_Fix
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("VerdanskGameBot.GameServer.GameServerModel", b =>
                {
                    b.Property<int>("ServerId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("AddedBy")
                        .IsRequired()
                        .HasColumnType("varchar(64)");

                    b.Property<DateTime>("AddedSince")
                        .HasColumnType("datetime(6)")
                        .HasColumnName("AddedSinceUTC");

                    b.Property<string>("ChannelId")
                        .IsRequired()
                        .HasColumnType("varchar(64)");

                    b.Property<string>("GameLink")
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<ushort>("GamePort")
                        .HasColumnType("smallint unsigned");

                    b.Property<string>("GameType")
                        .IsRequired()
                        .HasMaxLength(22)
                        .HasColumnType("varchar(22)");

                    b.Property<string>("IP")
                        .IsRequired()
                        .HasColumnType("varchar(45)");

                    b.Property<string>("LastModifiedBy")
                        .HasColumnType("varchar(64)");

                    b.Property<DateTime?>("LastModifiedSince")
                        .HasColumnType("datetime(6)")
                        .HasColumnName("LastModifiedSinceUTC");

                    b.Property<DateTime?>("LastOnline")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime(6)")
                        .HasDefaultValue(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc))
                        .HasColumnName("LastOnlineUTC");

                    b.Property<DateTime?>("LastUpdate")
                        .HasColumnType("datetime(6)")
                        .HasColumnName("LastUpdateUTC");

                    b.Property<string>("MessageId")
                        .IsRequired()
                        .HasColumnType("varchar(64)");

                    b.Property<string>("Note")
                        .HasColumnType("longtext");

                    b.Property<string>("Remarks")
                        .HasColumnType("longtext");

                    b.Property<string>("ServerName")
                        .IsRequired()
                        .HasMaxLength(22)
                        .HasColumnType("varchar(22)");

                    b.Property<TimeSpan>("UpdateInterval")
                        .HasColumnType("time(6)")
                        .HasColumnName("UpdateIntervalHMS");

                    b.HasKey("ServerId");

                    b.HasIndex(new[] { "ServerName" }, "IX_ServerName")
                        .IsUnique();

                    b.ToTable("GameServers");
                });
#pragma warning restore 612, 618
        }
    }
}
