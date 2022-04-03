﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VerdanskGameBot;

namespace VerdanskGameBot.Migrations
{
    [DbContext(typeof(GameServersDb))]
    [Migration("20220321005639_FixProps")]
    partial class FixProps
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "5.0.15");

            modelBuilder.Entity("VerdanskGameBot.GameServerModel", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasColumnName("id");

                    b.Property<ulong>("AddedBy")
                        .HasColumnType("INTEGER")
                        .HasColumnName("added_by");

                    b.Property<long>("AddedSince")
                        .HasColumnType("INTEGER")
                        .HasColumnName("added_since");

                    b.Property<ulong>("ChannelId")
                        .HasColumnType("INTEGER")
                        .HasColumnName("chan_id");

                    b.Property<string>("Description")
                        .HasMaxLength(200)
                        .HasColumnType("TEXT")
                        .HasColumnName("desc");

                    b.Property<string>("DisplayName")
                        .HasMaxLength(100)
                        .HasColumnType("TEXT")
                        .HasColumnName("display_name");

                    b.Property<string>("GameLink")
                        .HasMaxLength(100)
                        .HasColumnType("TEXT")
                        .HasColumnName("game_link");

                    b.Property<ushort>("GamePort")
                        .HasColumnType("INTEGER")
                        .HasColumnName("game_port");

                    b.Property<string>("IP")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("game_ip");

                    b.Property<string>("ImageUrl")
                        .HasMaxLength(200)
                        .HasColumnType("TEXT")
                        .HasColumnName("img_url");

                    b.Property<bool>("IsOnline")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasDefaultValue(false)
                        .HasColumnName("is_online");

                    b.Property<long>("LastOnline")
                        .HasColumnType("INTEGER")
                        .HasColumnName("last_online_time");

                    b.Property<long>("LastUpdate")
                        .HasColumnType("INTEGER")
                        .HasColumnName("last_update");

                    b.Property<ulong>("MessageId")
                        .HasColumnType("INTEGER")
                        .HasColumnName("msg_id");

                    b.Property<string>("Note")
                        .HasColumnType("TEXT")
                        .HasColumnName("note");

                    b.Property<string>("RconIP")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("rcon_ip");

                    b.Property<string>("RconPass")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("rcon_pass");

                    b.Property<ushort>("RconPort")
                        .HasColumnType("INTEGER")
                        .HasColumnName("rcon_port");

                    b.Property<string>("ServerName")
                        .IsRequired()
                        .HasMaxLength(22)
                        .HasColumnType("TEXT")
                        .HasColumnName("name");

                    b.Property<ulong>("UpdateInterval")
                        .HasColumnType("INTEGER")
                        .HasColumnName("update_interval");

                    b.HasKey("Id");

                    b.ToTable("GameServers");
                });
#pragma warning restore 612, 618
        }
    }
}
